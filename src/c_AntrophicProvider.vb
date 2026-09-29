Option Explicit On
Option Strict On

Imports System.IO
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading

' Implements the Anthropic Managed Agents provider used by Findit6Agent.
' It manages environments, agents, sessions, prompts and streamed responses.
Public NotInheritable Class AnthropicProvider
    Inherits AgentProviderBase

    Private ReadOnly _httpClient As HttpClient

    ' Creates the provider-wide HTTP client used for environment creation.
    ' Other requests currently keep their existing short-lived client behavior.
    Public Sub New()
        _httpClient = New HttpClient()
        _httpClient.Timeout = TimeSpan.FromSeconds(300)
    End Sub

    ' Establishes the Anthropic provider session in three steps.
    ' Existing environments and agents are reused while the working session is always new.
    Protected Overrides Async Function ProviderConnectAsync(cancellationToken As CancellationToken) As Task
        Await EnsureEnvironmentAsync(cancellationToken)
        Await EnsureAgentAsync(cancellationToken)
        Await CreateSessionAsync(cancellationToken)
    End Function

    ' Clears the provider-specific Anthropic session state.
    Protected Overrides Function ProviderDisconnectAsync(cancellationToken As CancellationToken) As Task
        cancellationToken.ThrowIfCancellationRequested()
        currentSession.SessionId = String.Empty
        Return Task.CompletedTask
    End Function

    ' Closes the current Anthropic working session locally.
    ' Environment and agent definitions remain available for later reuse.
    Public Overrides Function CloseSessionAsync(cancellationToken As CancellationToken) As Task
        Return ProviderDisconnectAsync(cancellationToken)
    End Function

    ' Reuses the current environment ID or creates a new Anthropic environment.
    ' The environment configuration is serialized as JSON instead of being assembled manually.
    Private Async Function EnsureEnvironmentAsync(cancellationToken As CancellationToken) As Task
        cancellationToken.ThrowIfCancellationRequested()

        If Not String.IsNullOrWhiteSpace(currentSession.EnvironmentId) Then
            LogDebug("Reusing existing Anthropic environment: " & currentSession.EnvironmentId)
            Return
        End If

        LogDebug("Creating Anthropic environment: " & currentSession.EnvironmentName)

        Dim oEnvironment = New With {
        .name = currentSession.EnvironmentName,
        .config = New With {.type = "cloud", .networking = New With {.type = "unrestricted"}}}

        Dim requestBody As String = JsonSerializer.Serialize(Of Object)(oEnvironment)

        Using request As New HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/environments")
            request.Headers.Add("x-api-key", currentSession.ApiKey.Trim())
            request.Headers.Add("anthropic-version", "2023-06-01")
            request.Headers.Add("anthropic-beta", "managed-agents-2026-04-01")
            request.Content = New StringContent(requestBody, Encoding.UTF8, "application/json")

            Using response As HttpResponseMessage = Await _httpClient.SendAsync(request, cancellationToken)
                Dim responseText As String = Await response.Content.ReadAsStringAsync()

                If Not response.IsSuccessStatusCode Then
                    LogDebug("[CreateEnvironment] ERROR Response: " & responseText)
                    Throw New Exception("Anthropic environment creation failed (" & response.StatusCode.ToString() & "): " & responseText)
                End If

                Using doc As JsonDocument = JsonDocument.Parse(responseText)
                    Dim root As JsonElement = doc.RootElement
                    Dim idElement As JsonElement

                    If Not root.TryGetProperty("id", idElement) Then
                        Throw New Exception("Anthropic environment response contains no id.")
                    End If

                    Dim environmentId As String = idElement.GetString()

                    If String.IsNullOrWhiteSpace(environmentId) Then
                        Throw New Exception("Anthropic environment response contains an empty id.")
                    End If

                    currentSession.EnvironmentId = environmentId
                    LogDebug("Anthropic environment ID: " & currentSession.EnvironmentId)
                End Using
            End Using
        End Using
    End Function

    ' Reuses the current agent ID or creates a new Anthropic agent.
    ' A valid agent ID is required before a session can be created.
    Private Async Function EnsureAgentAsync(cancellationToken As CancellationToken) As Task
        cancellationToken.ThrowIfCancellationRequested()

        LogDebug("Checking Anthropic agent: " & currentSession.AgentName)

        If Not String.IsNullOrWhiteSpace(currentSession.AgentId) Then
            LogDebug("Reusing existing Anthropic agent: " & currentSession.AgentId)
            Return
        End If

        LogDebug("Creating Anthropic agent: " & currentSession.AgentName)

        Dim sAgentInstructions As String = "You are a research agent. Follow the instructions provided in each session carefully and use the response format requested there."
        currentSession.AgentId = Await CreateAgentAsync(currentSession.AgentName, sAgentInstructions, cancellationToken)

        LogDebug("Anthropic agent ID: " & currentSession.AgentId)
    End Function

    ' Creates a new Anthropic agent and returns its ID.
    ' API and response errors are propagated so the original failure remains visible.
    Private Async Function CreateAgentAsync(agentName As String, systemRules As String, cancellationToken As CancellationToken) As Task(Of String)
        cancellationToken.ThrowIfCancellationRequested()

        Using client As New HttpClient()
            client.DefaultRequestHeaders.Add("x-api-key", currentSession.ApiKey.Trim())
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01")
            client.DefaultRequestHeaders.Add("anthropic-beta", "managed-agents-2026-04-01")

            Dim oAgent = New With {.name = agentName, .model = currentSession.Model, .system = systemRules, .environment_id = currentSession.EnvironmentId}
            Dim requestBody As String = JsonSerializer.Serialize(Of Object)(oAgent)

            LogDebug("[CreateAgent] Request: " & requestBody)

            Using content As New StringContent(requestBody, Encoding.UTF8, "application/json")
                Using response As HttpResponseMessage = Await client.PostAsync("https://api.anthropic.com/v1/agents", content, cancellationToken)
                    Dim responseText As String = Await response.Content.ReadAsStringAsync()

                    LogDebug("[CreateAgent] StatusCode: " & response.StatusCode.ToString())

                    If Not response.IsSuccessStatusCode Then
                        LogDebug("[CreateAgent] ERROR Response: " & responseText)
                        Throw New Exception("Anthropic agent creation failed (" & response.StatusCode.ToString() & "): " & responseText)
                    End If

                    Using doc As JsonDocument = JsonDocument.Parse(responseText)
                        Dim root As JsonElement = doc.RootElement
                        Dim idProp As JsonElement

                        If Not root.TryGetProperty("id", idProp) Then
                            Throw New Exception("Anthropic agent response contains no id.")
                        End If

                        Dim agentId As String = idProp.GetString()

                        If String.IsNullOrWhiteSpace(agentId) Then
                            Throw New Exception("Anthropic agent response contains an empty id.")
                        End If

                        LogDebug("[CreateAgent] Agent created: " & agentId)
                        Return agentId
                    End Using
                End Using
            End Using
        End Using
    End Function

    ' Creates a fresh Anthropic working session for the current agent and environment.
    ' Runtime token statistics are reset unless an internal working-session rollover requests otherwise.
    Private Async Function CreateSessionAsync(cancellationToken As CancellationToken, Optional resetRunStatistics As Boolean = True) As Task
        cancellationToken.ThrowIfCancellationRequested()
        LogDebug("Creating new Session")

        If String.IsNullOrWhiteSpace(currentSession.AgentId) Then Throw New Exception("CreateSessionAsync: agentId missing.")
        If String.IsNullOrWhiteSpace(currentSession.EnvironmentId) Then Throw New Exception("CreateSessionAsync: environmentId missing.")

        Using client As New HttpClient()
            client.DefaultRequestHeaders.Add("x-api-key", currentSession.ApiKey.Trim())
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01")
            client.DefaultRequestHeaders.Add("anthropic-beta", "managed-agents-2026-04-01")

            Dim oSession = New With {.agent = currentSession.AgentId, .environment_id = currentSession.EnvironmentId}
            Dim requestBody As String = JsonSerializer.Serialize(Of Object)(oSession)
            LogDebug("[CreateSession] Request: " & requestBody)

            Using content As New StringContent(requestBody, Encoding.UTF8, "application/json")
                Dim response As HttpResponseMessage = Await client.PostAsync("https://api.anthropic.com/v1/sessions", content, cancellationToken)
                Dim responseText As String = Await response.Content.ReadAsStringAsync()
                LogDebug("[CreateSession] StatusCode: " & response.StatusCode.ToString())
                LogDebug("[CreateSession] Response charcount: " & responseText.Length.ToString)

                If Not response.IsSuccessStatusCode Then
                    Throw New Exception("Session creation failed (HTTP " & CInt(response.StatusCode).ToString() & "): " & responseText)
                End If

                Using doc As JsonDocument = JsonDocument.Parse(responseText)
                    Dim root As JsonElement = doc.RootElement
                    Dim idProp As JsonElement
                    If Not root.TryGetProperty("id", idProp) Then Throw New Exception("CreateSessionAsync: Response enthält keine id.")

                    Dim sessionId As String = idProp.GetString()
                    If String.IsNullOrWhiteSpace(sessionId) Then Throw New Exception("CreateSessionAsync: Session-ID ist leer.")

                    currentSession.SessionId = sessionId
                    If resetRunStatistics Then
                        currentSession.SessionStartTime = DateTime.Now
                        currentSession.TokensIn = 0
                        currentSession.TokensOut = 0
                        currentSession.RateLimitWarningLevel = 0
                    End If

                    LogDebug("[CreateSession] NEW session created: " & currentSession.SessionId)
                End Using
            End Using
        End Using
    End Function

    ' Starts a fresh Anthropic working session while preserving accumulated run statistics.
    ' This is used when Chunk Mode needs a separate context for reading or synthesis work.
    Public Async Function StartNewWorkingSessionAsync(cancellationToken As CancellationToken) As Task
        Dim previousSessionId As String = currentSession.SessionId
        LogDebug("[Anthropic] Starting new working session. Previous SessionId: " & previousSessionId)
        Await CreateSessionAsync(cancellationToken, resetRunStatistics:=False)
        LogDebug("[Anthropic] New working SessionId: " & currentSession.SessionId)
    End Function

    ' Sends one user message to the current Anthropic session.
    ' The shared provider HttpClient is reused for the request.
    Public Overrides Async Function SendPromptAsync(
    prompt As String,
    cancellationToken As CancellationToken,
    Optional clearHistory As Boolean = False,
    Optional inferenceSettings As ProviderInferenceSettings = Nothing) As Task
        cancellationToken.ThrowIfCancellationRequested()

        If String.IsNullOrWhiteSpace(currentSession.SessionId) Then
            Throw New Exception("AnthropicProvider.SendPromptAsync: SessionId is missing.")
        End If

        Dim oEvent = New With {.type = "user.message", .content = New Object() {New With {.type = "text", .text = prompt}}}
        Dim oBody = New With {.events = New Object() {oEvent}}
        Dim requestBody As String = JsonSerializer.Serialize(Of Object)(oBody)
        Dim url As String = "https://api.anthropic.com/v1/sessions/" & currentSession.SessionId & "/events"

        Using request As New HttpRequestMessage(HttpMethod.Post, url)
            request.Headers.Add("x-api-key", currentSession.ApiKey.Trim())
            request.Headers.Add("anthropic-version", "2023-06-01")
            request.Headers.Add("anthropic-beta", "managed-agents-2026-04-01")
            request.Content = New StringContent(requestBody, Encoding.UTF8, "application/json")

            Using response As HttpResponseMessage = Await _httpClient.SendAsync(request, cancellationToken)

                If Not response.IsSuccessStatusCode Then
                    Dim errorText As String = Await response.Content.ReadAsStringAsync()

                    If errorText.Contains("Cannot send events to archived session", StringComparison.OrdinalIgnoreCase) Then
                        bContextLimit = True
                        LogDebug("Anthropic session is archived - bContextLimit set.")
                    End If

                    Throw New Exception("Anthropic SendPromptAsync failed (" & response.StatusCode.ToString() & "): " & errorText)
                End If

            End Using
        End Using
    End Function

    ' Reads the Anthropic event stream until the session becomes idle.
    ' Malformed stream events are skipped, while real session errors are propagated to the caller.
    Public Overrides Async Function ReceiveAsync(cancellationToken As CancellationToken) As Task(Of String)
        Try
            Dim streamUrl As String = "https://api.anthropic.com/v1/sessions/" & currentSession.SessionId & "/events/stream"
            LogDebug("Connecting to Anthropic event stream")

            Using request As New HttpRequestMessage(HttpMethod.Get, streamUrl)
                request.Headers.Add("x-api-key", currentSession.ApiKey.Trim())
                request.Headers.Add("anthropic-version", "2023-06-01")
                request.Headers.Add("anthropic-beta", "managed-agents-2026-04-01")

                Using response As HttpResponseMessage = Await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    If Not response.IsSuccessStatusCode Then
                        Dim errorText As String = Await response.Content.ReadAsStringAsync()
                        Throw New Exception("Anthropic stream failed (" & response.StatusCode.ToString() & "): " & errorText)
                    End If

                    LogDebug("Anthropic event stream connected")

                    Dim inputTokens As Integer = 0
                    Dim outputTokens As Integer = 0
                    Dim fullResponse As New StringBuilder()

                    Using responseStream = Await response.Content.ReadAsStreamAsync()
                        Using reader As New StreamReader(responseStream, Encoding.UTF8, True, 4096, False)
                            While Not reader.EndOfStream
                                cancellationToken.ThrowIfCancellationRequested()

                                Dim line As String = reader.ReadLine()
                                If String.IsNullOrWhiteSpace(line) Then Continue While
                                If Not line.StartsWith("data: ") Then Continue While

                                Dim jsonString As String = line.Substring(6)

                                Try
                                    Using doc As JsonDocument = JsonDocument.Parse(jsonString)
                                        Dim root As JsonElement = doc.RootElement
                                        Dim typeProp As JsonElement
                                        If Not root.TryGetProperty("type", typeProp) Then Continue While

                                        Select Case typeProp.GetString()
                                            Case "agent.message"
                                                Dim contentProp As JsonElement
                                                If root.TryGetProperty("content", contentProp) Then
                                                    For Each block As JsonElement In contentProp.EnumerateArray()
                                                        Dim blockType As JsonElement
                                                        If block.TryGetProperty("type", blockType) AndAlso blockType.GetString() = "text" Then
                                                            Dim textProp As JsonElement
                                                            If block.TryGetProperty("text", textProp) Then fullResponse.Append(textProp.GetString())
                                                        End If
                                                    Next
                                                End If

                                            Case "span.model_request_end"
                                                Dim usage As JsonElement
                                                If root.TryGetProperty("model_usage", usage) Then
                                                    Dim tmp As JsonElement
                                                    If usage.TryGetProperty("cache_creation_input_tokens", tmp) Then inputTokens += tmp.GetInt32()
                                                    If usage.TryGetProperty("cache_read_input_tokens", tmp) Then inputTokens += tmp.GetInt32()
                                                    If usage.TryGetProperty("input_tokens", tmp) Then inputTokens += tmp.GetInt32()
                                                    If usage.TryGetProperty("output_tokens", tmp) Then outputTokens += tmp.GetInt32()
                                                End If

                                            Case "session.status_idle"
                                                currentSession.TokensIn += inputTokens
                                                currentSession.TokensOut += outputTokens
                                                Return fullResponse.ToString()

                                            Case "session.error"
                                                Dim errorProp As JsonElement

                                                If root.TryGetProperty("error", errorProp) Then
                                                    Dim errorMsg As String = errorProp.ToString()

                                                    If errorMsg.Contains("rate_limited", StringComparison.OrdinalIgnoreCase) Then
                                                        currentSession.RateLimitWarningLevel = 10
                                                        Return ""
                                                    End If

                                                    If errorMsg.Contains("prompt is too long", StringComparison.OrdinalIgnoreCase) Then
                                                        bContextLimit = True
                                                        Return "{""action"":""answer"",""answer"":" & JsonSerializer.Serialize(CONTEXT_LIMIT_MESSAGE) & "}"
                                                    End If

                                                    Throw New Exception("Anthropic session error: " & errorMsg)
                                                End If

                                                Throw New Exception("Anthropic session returned an unspecified error.")
                                        End Select
                                    End Using

                                Catch ex As JsonException
                                    LogDebug("Anthropic event JSON parse error: " & ex.Message)
                                End Try
                            End While
                        End Using
                    End Using

                    currentSession.TokensIn += inputTokens
                    currentSession.TokensOut += outputTokens
                    Return fullResponse.ToString()
                End Using
            End Using

        Catch ex As OperationCanceledException
            LogDebug("[TIMEOUT] Anthropic ReceiveAsync cancelled")
            Throw

        Catch ex As Exception
            LogDebug("[ERROR] Anthropic ReceiveAsync: " & ex.Message)
            Throw
        End Try
    End Function


End Class

