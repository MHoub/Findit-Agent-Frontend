Option Explicit On
Option Strict On

Imports System.Collections.Concurrent
Imports System.Globalization
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Threading

Public NotInheritable Class OpenAICompatibleProvider
    Inherits AgentProviderBase

    Private Const SYSTEM_INSTRUCTIONS As String = "You are a research agent. Follow the instructions provided in each session carefully and use the response format requested there."

    Private ReadOnly _httpClient As HttpClient
    Private ReadOnly _history As New List(Of ChatMessage)
    Private ReadOnly _historyLock As New SemaphoreSlim(1, 1)
    Private ReadOnly _pendingResponses As New ConcurrentQueue(Of PendingCompletion)
    Private ReadOnly _pendingResponseSignal As New SemaphoreSlim(0)

    Private NotInheritable Class ChatMessage
        Public Property role As String = ""
        Public Property content As String = ""

        Public Sub New(ByVal messageRole As String, ByVal messageContent As String)
            role = messageRole
            content = messageContent
        End Sub
    End Class

    Private NotInheritable Class PendingCompletion
        Public Property CompletionTask As Task(Of String)
        Public Property Cancellation As CancellationTokenSource
    End Class

    Public Sub New()
        _httpClient = New HttpClient()
        _httpClient.Timeout = TimeSpan.FromSeconds(3600)
    End Sub

    Protected Overrides Async Function ProviderConnectAsync(cancellationToken As CancellationToken) As Task
        Await CreateSessionAsync(cancellationToken)
    End Function

    Protected Overrides Function ProviderDisconnectAsync(cancellationToken As CancellationToken) As Task
        cancellationToken.ThrowIfCancellationRequested()
        currentSession.SessionId = String.Empty
        ClearPendingResponses()

        _historyLock.Wait(cancellationToken)
        Try
            _history.Clear()
        Finally
            _historyLock.Release()
        End Try

        Return Task.CompletedTask
    End Function

    Public Overrides Function CloseSessionAsync(cancellationToken As CancellationToken) As Task
        Return ProviderDisconnectAsync(cancellationToken)
    End Function

    Private Async Function CreateSessionAsync(cancellationToken As CancellationToken, Optional resetRunStatistics As Boolean = True) As Task
        cancellationToken.ThrowIfCancellationRequested()

        If String.IsNullOrWhiteSpace(currentSession.BaseUrl) Then Throw New Exception("OpenAICompatibleProvider: BaseUrl is missing.")
        If String.IsNullOrWhiteSpace(currentSession.Model) Then Throw New Exception("OpenAICompatibleProvider: Model is missing.")
        If String.IsNullOrWhiteSpace(currentSession.ApiKey) Then Throw New Exception("OpenAICompatibleProvider: ApiKey is missing.")

        ClearPendingResponses()

        Await _historyLock.WaitAsync(cancellationToken)
        Try
            _history.Clear()
            _history.Add(New ChatMessage("system", SYSTEM_INSTRUCTIONS))
        Finally
            _historyLock.Release()
        End Try

        currentSession.SessionId = "openai-compatible-" & Guid.NewGuid().ToString("N")

        If resetRunStatistics Then
            currentSession.SessionStartTime = DateTime.Now
            currentSession.TokensIn = 0
            currentSession.TokensOut = 0
            currentSession.RateLimitWarningLevel = 0
        End If

        LogDebug("[OpenAICompatible] NEW local session: " & currentSession.SessionId)
        LogDebug("[OpenAICompatible] BaseUrl: " & currentSession.BaseUrl)
        LogDebug("[OpenAICompatible] Model: " & currentSession.Model)
    End Function

    Public Async Function StartNewWorkingSessionAsync(cancellationToken As CancellationToken) As Task
        Dim previousSessionId As String = currentSession.SessionId
        LogDebug("[OpenAICompatible] Starting new working session. Previous SessionId: " & previousSessionId)
        Await CreateSessionAsync(cancellationToken, resetRunStatistics:=False)
        LogDebug("[OpenAICompatible] New working SessionId: " & currentSession.SessionId)
    End Function

    Public Overrides Function SendPromptAsync(prompt As String, cancellationToken As CancellationToken, Optional clearHistory As Boolean = False, Optional inferenceSettings As ProviderInferenceSettings = Nothing) As Task
        cancellationToken.ThrowIfCancellationRequested()

        If String.IsNullOrWhiteSpace(currentSession.SessionId) Then Throw New Exception("OpenAICompatibleProvider.SendPromptAsync: SessionId is missing.")
        If String.IsNullOrWhiteSpace(currentSession.BaseUrl) Then Throw New Exception("OpenAICompatibleProvider.SendPromptAsync: BaseUrl is missing.")
        If String.IsNullOrWhiteSpace(currentSession.Model) Then Throw New Exception("OpenAICompatibleProvider.SendPromptAsync: Model is missing.")
        If String.IsNullOrWhiteSpace(currentSession.ApiKey) Then Throw New Exception("OpenAICompatibleProvider.SendPromptAsync: ApiKey is missing.")

        Dim oCancellation As CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)

        Dim oPending As New PendingCompletion With {
        .Cancellation = oCancellation,
        .CompletionTask = ExecuteCompletionAsync(prompt, clearHistory, inferenceSettings, oCancellation.Token)
    }

        _pendingResponses.Enqueue(oPending)
        _pendingResponseSignal.Release()

        Return Task.CompletedTask

    End Function

    Public Sub RemoveFromHistory(ByVal sText As String)

        If String.IsNullOrEmpty(sText) Then Exit Sub
        Const sReplacement As String =
    "IMPORTANT - HISTORY COMPACTION NOTICE:" & vbCrLf &
    "Previously supplied source material was removed from the conversation history to save tokens." & vbCrLf &
    "Task-relevant information already extracted from it was preserved in the Ledger." & vbCrLf &
    "THE UNDERLYING SOURCES AND SEARCH RESULTS ARE STILL AVAILABLE." & vbCrLf &
    "If additional information is needed, you may retrieve it again:" & vbCrLf &
    "- request a previously read complete file again with request_file;" & vbCrLf &
    "- recreate previously supplied Context Excerpts by repeating an appropriate FullSearch or HitlistSearch with Context Excerpts enabled." & vbCrLf &
    "Do not assume that removed source material is permanently unavailable."
        _historyLock.Wait()
        Try
            For i As Integer = _history.Count - 1 To 0 Step -1
                If _history(i).role.Equals("user", StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(_history(i).content, sText, StringComparison.Ordinal) Then
                    _history(i).content = sReplacement
                    LogDebug("[OpenAICompatible] Statistic source replaced by history placeholder.")
                    Exit For
                End If
            Next
        Finally
            _historyLock.Release()
        End Try
    End Sub

    Public Overrides Async Function ReceiveAsync(cancellationToken As CancellationToken) As Task(Of String)
        Dim oPending As PendingCompletion = Nothing

        Try
            Await _pendingResponseSignal.WaitAsync(cancellationToken)

            If Not _pendingResponses.TryDequeue(oPending) OrElse oPending Is Nothing Then
                Throw New Exception("OpenAICompatibleProvider.ReceiveAsync: no pending response is available.")
            End If

            Return Await oPending.CompletionTask.WaitAsync(cancellationToken)

        Catch ex As OperationCanceledException

            LogDebug("[TIMEOUT] OpenAICompatible ReceiveAsync cancelled | ExternalToken=" &
             cancellationToken.IsCancellationRequested.ToString() &
             " | PendingToken=" &
             If(oPending IsNot Nothing AndAlso oPending.Cancellation IsNot Nothing,
                oPending.Cancellation.IsCancellationRequested.ToString(),
                "n/a"))

            If oPending IsNot Nothing AndAlso oPending.Cancellation IsNot Nothing Then
                oPending.Cancellation.Cancel()
            End If
            LogDebug("[TIMEOUT] OpenAICompatible ReceiveAsync cancelled")
            Throw

        Catch ex As Exception
            LogDebug("[ERROR] OpenAICompatible ReceiveAsync: " & ex.Message)
            Throw

        Finally
            If oPending IsNot Nothing AndAlso oPending.Cancellation IsNot Nothing Then oPending.Cancellation.Dispose()
        End Try
    End Function

    Private Async Function ExecuteCompletionAsync(
    prompt As String,
    clearHistory As Boolean,
    inferenceSettings As ProviderInferenceSettings,
    cancellationToken As CancellationToken) As Task(Of String)
        Await _historyLock.WaitAsync(cancellationToken)

        Dim bUserMessageStored As Boolean = False

        Try
            If clearHistory Then
                _history.Clear()
                _history.Add(New ChatMessage("system", SYSTEM_INSTRUCTIONS))
            End If

            _history.Add(New ChatMessage("user", prompt))
            bUserMessageStored = True

            Dim oBody As New Dictionary(Of String, Object) From {
    {"model", currentSession.Model},
    {"messages", _history.ToArray()},
    {"stream", False}
}

            ApplyInferenceSettings(oBody, inferenceSettings)

            Dim requestBody As String = JsonSerializer.Serialize(oBody)
            Dim url As String = GetChatCompletionsUrl()

            LogDebug("[OpenAICompatible] Sending chat completion to: " & url)

            Using request As New HttpRequestMessage(HttpMethod.Post, url)
                request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", currentSession.ApiKey.Trim())
                request.Content = New StringContent(requestBody, Encoding.UTF8, "application/json")

                Using response As HttpResponseMessage = Await _httpClient.SendAsync(request, cancellationToken)
                    Dim responseText As String = Await response.Content.ReadAsStringAsync()

                    If Not response.IsSuccessStatusCode Then
                        If CInt(response.StatusCode) = 429 Then
                            currentSession.RateLimitWarningLevel = 10
                            RemoveLastUserMessage()
                            bUserMessageStored = False
                            LogDebug("[OpenAICompatible] Rate limit reached.")
                            Return ""
                        End If

                        If IsContextLimitError(responseText) Then
                            bContextLimit = True
                            LogDebug("[OpenAICompatible] Context limit reached - bContextLimit set.")
                            Return BuildContextLimitAnswer()
                        End If

                        RemoveLastUserMessage()
                        bUserMessageStored = False
                        Throw New Exception("OpenAI-compatible chat completion failed (" & response.StatusCode.ToString() & "): " & responseText)
                    End If

                    Using doc As JsonDocument = JsonDocument.Parse(responseText)
                        Dim root As JsonElement = doc.RootElement

                        AddUsage(root)

                        Dim sAssistantText As String = ExtractAssistantText(root)

                        If String.IsNullOrWhiteSpace(sAssistantText) Then
                            RemoveLastUserMessage()
                            bUserMessageStored = False
                            Throw New Exception("OpenAI-compatible response contains no assistant text.")
                        End If

                        _history.Add(New ChatMessage("assistant", sAssistantText))
                        bUserMessageStored = False

                        LogDebug("[OpenAICompatible] Response received | Chars: " & sAssistantText.Length.ToString())
                        Return sAssistantText
                    End Using
                End Using
            End Using

        Catch ex As OperationCanceledException
            If bUserMessageStored Then RemoveLastUserMessage()
            Throw

        Catch
            If bUserMessageStored Then RemoveLastUserMessage()
            Throw

        Finally
            _historyLock.Release()
        End Try
    End Function

    Private Function GetChatCompletionsUrl() As String
        Dim sBaseUrl As String = currentSession.BaseUrl.Trim().TrimEnd("/"c)

        If sBaseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) Then Return sBaseUrl

        Return sBaseUrl & "/chat/completions"
    End Function

    Private Function ExtractAssistantText(ByVal root As JsonElement) As String
        Dim choices As JsonElement

        If Not root.TryGetProperty("choices", choices) OrElse choices.ValueKind <> JsonValueKind.Array OrElse choices.GetArrayLength() = 0 Then Return ""

        Dim firstChoice As JsonElement = choices(0)
        Dim message As JsonElement

        If Not firstChoice.TryGetProperty("message", message) OrElse message.ValueKind <> JsonValueKind.Object Then Return ""

        Dim content As JsonElement
        If Not message.TryGetProperty("content", content) Then Return ""

        If content.ValueKind = JsonValueKind.String Then Return If(content.GetString(), "")
        If content.ValueKind <> JsonValueKind.Array Then Return ""

        Dim sb As New StringBuilder

        For Each block As JsonElement In content.EnumerateArray()
            If block.ValueKind = JsonValueKind.String Then
                sb.Append(block.GetString())
                Continue For
            End If

            If block.ValueKind <> JsonValueKind.Object Then Continue For

            Dim textProp As JsonElement
            If block.TryGetProperty("text", textProp) Then
                If textProp.ValueKind = JsonValueKind.String Then
                    sb.Append(textProp.GetString())
                ElseIf textProp.ValueKind = JsonValueKind.Object Then
                    Dim valueProp As JsonElement
                    If textProp.TryGetProperty("value", valueProp) AndAlso valueProp.ValueKind = JsonValueKind.String Then sb.Append(valueProp.GetString())
                End If
            End If
        Next

        Return sb.ToString()
    End Function

    Private Sub AddUsage(ByVal root As JsonElement)
        Dim usage As JsonElement

        If Not root.TryGetProperty("usage", usage) OrElse usage.ValueKind <> JsonValueKind.Object Then Return

        Dim iInput As Integer = GetIntegerProperty(usage, "prompt_tokens")
        Dim iOutput As Integer = GetIntegerProperty(usage, "completion_tokens")

        If iInput = 0 Then iInput = GetIntegerProperty(usage, "input_tokens")
        If iOutput = 0 Then iOutput = GetIntegerProperty(usage, "output_tokens")

        currentSession.TokensIn += iInput
        currentSession.TokensOut += iOutput
    End Sub

    Private Function GetIntegerProperty(ByVal element As JsonElement, ByVal propertyName As String) As Integer
        Dim value As JsonElement

        If element.ValueKind = JsonValueKind.Object AndAlso element.TryGetProperty(propertyName, value) AndAlso value.ValueKind = JsonValueKind.Number Then
            Dim iValue As Integer
            If value.TryGetInt32(iValue) Then Return iValue
        End If

        Return 0
    End Function

    Private Function IsContextLimitError(ByVal errorText As String) As Boolean
        If String.IsNullOrWhiteSpace(errorText) Then Return False

        Return errorText.IndexOf("context_length_exceeded", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("maximum context length", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("context window", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("input is too long", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("input too long", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("too many tokens", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               errorText.IndexOf("token limit", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Function BuildContextLimitAnswer() As String
        Return "{""action"":""answer"",""answer"":" & JsonSerializer.Serialize(CONTEXT_LIMIT_MESSAGE) & "}"
    End Function

    Private Sub RemoveLastUserMessage()
        If _history.Count = 0 Then Return

        Dim lastMessage As ChatMessage = _history(_history.Count - 1)
        If lastMessage.role.Equals("user", StringComparison.OrdinalIgnoreCase) Then _history.RemoveAt(_history.Count - 1)
    End Sub

    Private Sub ClearPendingResponses()
        Dim oPending As PendingCompletion = Nothing

        While _pendingResponses.TryDequeue(oPending)
            If oPending IsNot Nothing AndAlso oPending.Cancellation IsNot Nothing Then
                oPending.Cancellation.Cancel()
                oPending.Cancellation.Dispose()
            End If
        End While

        While _pendingResponseSignal.Wait(0)
        End While
    End Sub

    Private Sub ApplyInferenceSettings(body As Dictionary(Of String, Object),
                                   inferenceSettings As ProviderInferenceSettings)

        If inferenceSettings Is Nothing Then Return

        Dim provider As String = currentSession.Provider.Trim().ToLowerInvariant()
        Dim model As String = currentSession.Model.Trim().ToLowerInvariant()

        Dim reasoningEffort As String = ""
        Dim sendTemperature As Boolean = False
        Dim supported As Boolean = False

        Select Case provider

            Case "mistral"

                Select Case model

                    Case "mistral-medium-3-5", "mistral-small-latest"
                        supported = True
                        sendTemperature = True
                        reasoningEffort = If(inferenceSettings.Think, "high", "none")

                End Select


            Case "groq"

                If model.Contains("qwen3.8") Then
                    supported = True
                    sendTemperature = True
                    reasoningEffort = If(inferenceSettings.Think, "high", "none")

                ElseIf model.Contains("qwen3.6") Then
                    supported = True
                    sendTemperature = True
                    reasoningEffort = If(inferenceSettings.Think, "default", "none")

                ElseIf model.Contains("gpt-oss") Then
                    supported = True
                    sendTemperature = True

                    ' GPT-OSS reasoning cannot really be switched off.
                    reasoningEffort = If(inferenceSettings.Think, "high", "low")
                End If


            Case "scaleway"

                If model.Contains("qwen3.6") OrElse
               model.Contains("qwen3.5") OrElse
               model.Contains("gemma-4") Then

                    supported = True
                    sendTemperature = True
                    reasoningEffort = If(inferenceSettings.Think, "high", "none")

                ElseIf model.Contains("mistral-medium-3.5") OrElse
                   model.Contains("mistral-medium-3-5") Then

                    supported = True
                    sendTemperature = True
                    reasoningEffort = If(inferenceSettings.Think, "high", "none")

                ElseIf model.Contains("glm-5.2") Then

                    supported = True
                    sendTemperature = True
                    reasoningEffort = If(inferenceSettings.Think, "high", "none")

                ElseIf model.Contains("gpt-oss") Then

                    supported = True
                    sendTemperature = True
                    reasoningEffort = If(inferenceSettings.Think, "high", "low")

                End If


            Case "grok"

                If model.StartsWith("grok-4.3") Then

                    supported = True
                    sendTemperature = True
                    reasoningEffort = If(inferenceSettings.Think, "high", "none")

                ElseIf model.StartsWith("grok-4.5") OrElse
                   model.StartsWith("grok-4.6") Then

                    supported = True
                    sendTemperature = True

                    ' These models always reason; "low" is our equivalent of Think=False.
                    reasoningEffort = If(inferenceSettings.Think, "high", "low")

                End If


            Case "gemini"

                If model.StartsWith("gemini-3.") Then

                    supported = True

                    ' Gemini 3 should normally keep its provider-recommended temperature.
                    sendTemperature = False

                    ' Thinking cannot really be disabled.
                    reasoningEffort = If(inferenceSettings.Think, "high", "low")

                End If

        End Select


        If Not supported Then

            LogDebug("[WARN] OpenAICompatible inference settings not applied. " &
                 "Compatibility is not verified for Provider=" &
                 currentSession.Provider &
                 " Model=" &
                 currentSession.Model &
                 ". Provider/model defaults will be used.")

            Return
        End If


        If sendTemperature Then
            body("temperature") = inferenceSettings.Temperature
        End If

        If reasoningEffort <> "" Then
            body("reasoning_effort") = reasoningEffort
        End If


        LogDebug("[OpenAICompatible] Inference | Provider=" &
             currentSession.Provider &
             " | Model=" &
             currentSession.Model &
             " | Think=" &
             inferenceSettings.Think.ToString() &
             " | reasoning_effort=" &
             reasoningEffort &
             " | Temp=" &
             If(sendTemperature,
                inferenceSettings.Temperature.ToString(CultureInfo.InvariantCulture),
                "<provider default>"))

    End Sub

End Class
