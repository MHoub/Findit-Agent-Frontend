Option Explicit On
Option Strict On

Imports System.Collections.Concurrent
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Threading


' Implements the OpenAI Responses API provider used by Findit6Agent.
' It maps OpenAI conversations and background responses onto the existing
' AgentProviderBase SendPromptAsync / ReceiveAsync contract.
Public NotInheritable Class OpenAIProvider
    Inherits AgentProviderBase

    Private ReadOnly Property API_BASE As String
        Get
            Return AgentsIni.ReadValue("Provider." & currentSession.Provider, "BaseUrl").TrimEnd("/"c)
        End Get
    End Property
    Private Const OPENAI_SYSTEM_INSTRUCTIONS As String = "You are a research agent. Follow the instructions provided in each session carefully and use the response format requested there."

    Private ReadOnly _httpClient As HttpClient
    Private ReadOnly _pendingResponses As New ConcurrentQueue(Of String)()
    Private ReadOnly _pendingResponseSignal As New SemaphoreSlim(0)
    Private _previousResponseId As String = ""

    Private ReadOnly _perplexityRequestLock As New SemaphoreSlim(1, 1)
    Private _lastPerplexityRequest As DateTime = DateTime.MinValue
    Private Const PERPLEXITY_REQUEST_INTERVAL As Integer = 1500

    Public Sub New()
        _httpClient = New HttpClient()
        _httpClient.Timeout = TimeSpan.FromSeconds(300)
    End Sub

    Protected Overrides Async Function ProviderConnectAsync(cancellationToken As CancellationToken) As Task
        Await CreateConversationAsync(cancellationToken)
    End Function

    Protected Overrides Function ProviderDisconnectAsync(cancellationToken As CancellationToken) As Task
        cancellationToken.ThrowIfCancellationRequested()
        currentSession.SessionId = String.Empty
        ClearPendingResponses()
        Return Task.CompletedTask
    End Function

    Public Overrides Function CloseSessionAsync(cancellationToken As CancellationToken) As Task
        Return ProviderDisconnectAsync(cancellationToken)
    End Function

    Private Async Function WaitForPerplexityRequestAsync(cancellationToken As CancellationToken) As Task

        If Not currentSession.Provider.Equals("Perplexity", StringComparison.OrdinalIgnoreCase) Then Return

        Await _perplexityRequestLock.WaitAsync(cancellationToken)

        Try

            If _lastPerplexityRequest <> DateTime.MinValue Then

                Dim elapsed As Double = (DateTime.UtcNow - _lastPerplexityRequest).TotalMilliseconds
                Dim iWait As Integer = CInt(Math.Ceiling(PERPLEXITY_REQUEST_INTERVAL - elapsed))

                If iWait > 0 Then
                    Await Task.Delay(iWait, cancellationToken)
                End If

            End If

            _lastPerplexityRequest = DateTime.UtcNow

        Finally
            _perplexityRequestLock.Release()
        End Try

    End Function

    Private Async Function CreateConversationAsync(cancellationToken As CancellationToken, Optional resetRunStatistics As Boolean = True) As Task

        cancellationToken.ThrowIfCancellationRequested()

        ' Perplexity uses the Responses API but does not require an OpenAI conversation.
        If currentSession.Provider.Equals("Perplexity", StringComparison.OrdinalIgnoreCase) Then

            currentSession.SessionId = ""
            ClearPendingResponses()
            _previousResponseId = ""
            If resetRunStatistics Then
                currentSession.SessionStartTime = DateTime.Now
                currentSession.TokensIn = 0
                currentSession.TokensOut = 0
                currentSession.RateLimitWarningLevel = 0
            End If

            LogDebug("[Perplexity] No conversation creation required")
            Return

        End If

        LogDebug("Creating new OpenAI conversation")

        Dim oDeveloperMessage = New With {
        .type = "message",
        .role = "developer",
        .content = OPENAI_SYSTEM_INSTRUCTIONS
    }

        Dim oConversation = New With {
        .items = New Object() {oDeveloperMessage}
    }

        Dim requestBody As String = JsonSerializer.Serialize(Of Object)(oConversation)

        Using request As New HttpRequestMessage(HttpMethod.Post, API_BASE & "/conversations")

            AddAuthorizationHeader(request)
            request.Content = New StringContent(requestBody, Encoding.UTF8, "application/json")

            Using response As HttpResponseMessage = Await _httpClient.SendAsync(request, cancellationToken)

                Dim responseText As String = Await response.Content.ReadAsStringAsync()

                If Not response.IsSuccessStatusCode Then
                    HandleHttpError(response, responseText, "OpenAI conversation creation failed")
                End If

                Using doc As JsonDocument = JsonDocument.Parse(responseText)

                    Dim root As JsonElement = doc.RootElement
                    Dim idProp As JsonElement

                    If Not root.TryGetProperty("id", idProp) OrElse idProp.ValueKind <> JsonValueKind.String Then
                        Throw New Exception("OpenAI conversation response contains no id.")
                    End If

                    Dim conversationId As String = If(idProp.GetString(), "").Trim()

                    If conversationId.Length = 0 Then
                        Throw New Exception("OpenAI conversation response contains an empty id.")
                    End If

                    currentSession.SessionId = conversationId
                    ClearPendingResponses()

                    If resetRunStatistics Then
                        currentSession.SessionStartTime = DateTime.Now
                        currentSession.TokensIn = 0
                        currentSession.TokensOut = 0
                        currentSession.RateLimitWarningLevel = 0
                    End If

                    LogDebug("[OpenAI] NEW conversation created: " & currentSession.SessionId)

                End Using
            End Using
        End Using

    End Function

    Public Async Function StartNewWorkingSessionAsync(cancellationToken As CancellationToken) As Task
        Dim previousSessionId As String = currentSession.SessionId
        LogDebug("[OpenAI] Starting new working session. Previous SessionId: " & previousSessionId)
        Await CreateConversationAsync(cancellationToken, resetRunStatistics:=False)
        LogDebug("[OpenAI] New working SessionId: " & currentSession.SessionId)
    End Function

    Public Overrides Async Function SendPromptAsync(
    prompt As String,
    cancellationToken As CancellationToken,
    Optional clearHistory As Boolean = False,
    Optional inferenceSettings As ProviderInferenceSettings = Nothing) As Task

        cancellationToken.ThrowIfCancellationRequested()

        Dim bPerplexity As Boolean = currentSession.Provider.Equals("Perplexity", StringComparison.OrdinalIgnoreCase)

        If Not bPerplexity AndAlso String.IsNullOrWhiteSpace(currentSession.SessionId) Then
            Throw New Exception("OpenAIProvider.SendPromptAsync: SessionId is missing.")
        End If

        If String.IsNullOrWhiteSpace(currentSession.Model) Then
            Throw New Exception("OpenAIProvider.SendPromptAsync: Model is missing.")
        End If

        Dim requestBody As String

        If bPerplexity Then

            If clearHistory Then _previousResponseId = ""

            Dim oBody As New Dictionary(Of String, Object) From {
            {"model", currentSession.Model},
            {"input", prompt},
            {"instructions", OPENAI_SYSTEM_INSTRUCTIONS},
            {"store", True},
            {"truncation", "disabled"}
        }

            If Not String.IsNullOrWhiteSpace(_previousResponseId) Then
                oBody.Add("previous_response_id", _previousResponseId)
            End If

            requestBody = JsonSerializer.Serialize(oBody)

        Else

            Dim oBody = New With {
            .model = currentSession.Model,
            .conversation = currentSession.SessionId,
            .input = prompt,
            .background = True,
            .store = True,
            .truncation = "disabled"
        }

            requestBody = JsonSerializer.Serialize(Of Object)(oBody)

        End If

        For iAttempt As Integer = 1 To 5

            cancellationToken.ThrowIfCancellationRequested()

            Using request As New HttpRequestMessage(HttpMethod.Post, API_BASE & "/responses")

                AddAuthorizationHeader(request)
                request.Content = New StringContent(requestBody, Encoding.UTF8, "application/json")

                Await WaitForPerplexityRequestAsync(cancellationToken)

                Using response As HttpResponseMessage = Await _httpClient.SendAsync(request, cancellationToken)

                    Dim responseText As String = Await response.Content.ReadAsStringAsync()

                    If Not response.IsSuccessStatusCode Then

                        If responseText.Contains("conversation_locked", StringComparison.OrdinalIgnoreCase) AndAlso iAttempt < 5 Then
                            LogDebug("[OpenAI] Conversation still locked - retry " & iAttempt.ToString() & "/4")
                            Await Task.Delay(2000, cancellationToken)
                            Continue For
                        End If

                        HandleHttpError(response, responseText, "OpenAI SendPromptAsync failed")

                    End If

                    Using doc As JsonDocument = JsonDocument.Parse(responseText)

                        Dim root As JsonElement = doc.RootElement
                        Dim idProp As JsonElement

                        If Not root.TryGetProperty("id", idProp) OrElse idProp.ValueKind <> JsonValueKind.String Then
                            Throw New Exception("OpenAI response creation returned no response id.")
                        End If

                        Dim responseId As String = If(idProp.GetString(), "").Trim()

                        If responseId.Length = 0 Then
                            Throw New Exception("OpenAI response creation returned an empty response id.")
                        End If

                        If bPerplexity Then
                            _previousResponseId = responseId
                        End If

                        _pendingResponses.Enqueue(responseId)
                        _pendingResponseSignal.Release()

                        Dim statusText As String = GetStringProperty(root, "status")

                        LogDebug("[" & currentSession.Provider & "] Response created: " &
                             responseId &
                             If(statusText.Length > 0, " | Status: " & statusText, ""))

                        Return

                    End Using

                End Using

            End Using

        Next

    End Function

    Public Overrides Async Function ReceiveAsync(cancellationToken As CancellationToken) As Task(Of String)

        Try

            Await _pendingResponseSignal.WaitAsync(cancellationToken)

            Dim responseId As String = Nothing

            If Not _pendingResponses.TryDequeue(responseId) OrElse String.IsNullOrWhiteSpace(responseId) Then
                Throw New Exception("OpenAI ReceiveAsync: no pending response id is available.")
            End If

            LogDebug("Waiting for OpenAI response: " & responseId)

            ' Perplexity has stricter request rate limits.
            If currentSession.Provider.Equals("Perplexity", StringComparison.OrdinalIgnoreCase) Then
                Await Task.Delay(1200, cancellationToken)
            End If

            Do

                cancellationToken.ThrowIfCancellationRequested()

                Using request As New HttpRequestMessage(HttpMethod.Get, API_BASE & "/responses/" & responseId)

                    AddAuthorizationHeader(request)

                    Await WaitForPerplexityRequestAsync(cancellationToken)

                    Using response As HttpResponseMessage = Await _httpClient.SendAsync(request, cancellationToken)

                        Dim responseText As String = Await response.Content.ReadAsStringAsync()

                        If Not response.IsSuccessStatusCode Then

                            If response.StatusCode = HttpStatusCode.TooManyRequests AndAlso
                               currentSession.Provider.Equals("Perplexity", StringComparison.OrdinalIgnoreCase) Then

                                LogDebug("[Perplexity] Rate limit while retrieving response - waiting 1500 ms")
                                Await Task.Delay(1500, cancellationToken)
                                Continue Do

                            End If

                            HandleHttpError(response, responseText, "OpenAI response retrieval failed")

                        End If

                        Using doc As JsonDocument = JsonDocument.Parse(responseText)

                            Dim root As JsonElement = doc.RootElement
                            Dim statusText As String = GetStringProperty(root, "status").ToLowerInvariant()

                            Select Case statusText

                                Case "queued", "in_progress", ""

                                Case "completed"

                                    AddUsage(root)

                                    Dim sText As String = ExtractOutputText(root)

                                    LogDebug("[" & currentSession.Provider & "] Response completed: " &
                                             responseId &
                                             " | Chars: " &
                                             sText.Length.ToString())

                                    Return sText

                                Case "incomplete"

                                    AddUsage(root)

                                    Dim reason As String = GetIncompleteReason(root)
                                    Dim sText As String = ExtractOutputText(root)

                                    LogDebug("[" & currentSession.Provider & "] Response incomplete: " &
                                             responseId &
                                             If(reason.Length > 0, " | Reason: " & reason, ""))

                                    If IsContextLimitError(reason) Then
                                        bContextLimit = True
                                        Return BuildContextLimitAnswer()
                                    End If

                                    If sText.Length > 0 Then
                                        Return sText
                                    End If

                                    Throw New Exception("OpenAI response incomplete" &
                                                        If(reason.Length > 0, ": " & reason, "."))

                                Case "failed"

                                    AddUsage(root)

                                    Dim errorText As String = GetResponseError(root)

                                    If IsContextLimitError(errorText) Then
                                        bContextLimit = True
                                        LogDebug("OpenAI context limit reached - bContextLimit set.")
                                        Return BuildContextLimitAnswer()
                                    End If

                                    Throw New Exception("OpenAI response failed: " &
                                                        If(errorText.Length > 0, errorText, "unspecified error"))

                                Case "cancelled"

                                    AddUsage(root)

                                    Throw New Exception("OpenAI response was cancelled by the provider.")

                                Case Else

                                    Throw New Exception("OpenAI response returned unknown status '" &
                                                        statusText &
                                                        "'.")

                            End Select

                        End Using

                    End Using

                End Using

                If currentSession.Provider.Equals("Perplexity", StringComparison.OrdinalIgnoreCase) Then
                    Await Task.Delay(1500, cancellationToken)
                Else
                    Await Task.Delay(500, cancellationToken)
                End If

            Loop
        Catch ex As OperationCanceledException

            LogDebug("[TIMEOUT] OpenAI ReceiveAsync cancelled")
            Throw

        Catch ex As Exception

            LogDebug("[ERROR] OpenAI ReceiveAsync: " & ex.Message)
            Throw

        End Try

    End Function
    Private Sub AddAuthorizationHeader(ByVal request As HttpRequestMessage)
        request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", currentSession.ApiKey.Trim())
    End Sub

    Private Sub AddUsage(ByVal root As JsonElement)
        Dim usage As JsonElement
        If Not root.TryGetProperty("usage", usage) OrElse usage.ValueKind <> JsonValueKind.Object Then Return

        Dim tmp As JsonElement
        If usage.TryGetProperty("input_tokens", tmp) AndAlso tmp.ValueKind = JsonValueKind.Number Then currentSession.TokensIn += tmp.GetInt32()
        If usage.TryGetProperty("output_tokens", tmp) AndAlso tmp.ValueKind = JsonValueKind.Number Then currentSession.TokensOut += tmp.GetInt32()
    End Sub

    Private Function ExtractOutputText(ByVal root As JsonElement) As String
        Dim outputText As JsonElement
        If root.TryGetProperty("output_text", outputText) AndAlso outputText.ValueKind = JsonValueKind.String Then Return If(outputText.GetString(), "")

        Dim sb As New StringBuilder
        Dim output As JsonElement
        If Not root.TryGetProperty("output", output) OrElse output.ValueKind <> JsonValueKind.Array Then Return ""

        For Each item As JsonElement In output.EnumerateArray()
            Dim content As JsonElement
            If Not item.TryGetProperty("content", content) OrElse content.ValueKind <> JsonValueKind.Array Then Continue For

            For Each block As JsonElement In content.EnumerateArray()
                Dim blockType As String = GetStringProperty(block, "type")

                If blockType.Equals("output_text", StringComparison.OrdinalIgnoreCase) Then
                    Dim textProp As JsonElement
                    If block.TryGetProperty("text", textProp) AndAlso textProp.ValueKind = JsonValueKind.String Then sb.Append(textProp.GetString())

                ElseIf blockType.Equals("refusal", StringComparison.OrdinalIgnoreCase) Then
                    Dim refusalProp As JsonElement
                    If block.TryGetProperty("refusal", refusalProp) AndAlso refusalProp.ValueKind = JsonValueKind.String Then sb.Append(refusalProp.GetString())
                End If
            Next
        Next

        Return sb.ToString()
    End Function

    Private Function GetResponseError(ByVal root As JsonElement) As String
        Dim errorProp As JsonElement
        If Not root.TryGetProperty("error", errorProp) OrElse errorProp.ValueKind = JsonValueKind.Null Then Return ""

        If errorProp.ValueKind = JsonValueKind.Object Then
            Dim code As String = GetStringProperty(errorProp, "code")
            Dim message As String = GetStringProperty(errorProp, "message")

            If code.Length > 0 AndAlso message.Length > 0 Then Return code & ": " & message
            If message.Length > 0 Then Return message
            If code.Length > 0 Then Return code
        End If

        Return errorProp.ToString()
    End Function

    Private Function GetIncompleteReason(ByVal root As JsonElement) As String
        Dim incomplete As JsonElement
        If Not root.TryGetProperty("incomplete_details", incomplete) OrElse incomplete.ValueKind <> JsonValueKind.Object Then Return ""
        Return GetStringProperty(incomplete, "reason")
    End Function

    Private Function GetStringProperty(ByVal element As JsonElement, ByVal propertyName As String) As String
        Dim value As JsonElement

        If element.ValueKind = JsonValueKind.Object AndAlso
           element.TryGetProperty(propertyName, value) AndAlso
           value.ValueKind = JsonValueKind.String Then

            Return If(value.GetString(), "")
        End If

        Return ""
    End Function

    Private Function IsContextLimitError(ByVal errorText As String) As Boolean
        If String.IsNullOrWhiteSpace(errorText) Then Return False

        Return errorText.IndexOf("context_length_exceeded", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("maximum context length", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("context window", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("input is too long", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("input too long", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("too many tokens", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Function BuildContextLimitAnswer() As String
        Return "{""action"":""answer"",""answer"":" & JsonSerializer.Serialize(CONTEXT_LIMIT_MESSAGE) & "}"
    End Function

    Private Sub HandleHttpError(ByVal response As HttpResponseMessage, ByVal responseText As String, ByVal prefix As String)
        If CInt(response.StatusCode) = 429 Then currentSession.RateLimitWarningLevel = 10

        If IsContextLimitError(responseText) Then
            bContextLimit = True
            LogDebug("OpenAI context limit reached - bContextLimit set.")
        End If

        Throw New Exception(prefix & " (" & response.StatusCode.ToString() & "): " & responseText)
    End Sub

    Private Sub ClearPendingResponses()
        Dim responseId As String = Nothing

        While _pendingResponses.TryDequeue(responseId)
        End While

        While _pendingResponseSignal.Wait(0)
        End While
    End Sub

End Class
