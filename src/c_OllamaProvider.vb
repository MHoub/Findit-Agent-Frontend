Option Explicit On
Option Strict On

Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Globalization

Public NotInheritable Class c_OllamaProvider
    Inherits AgentProviderBase

    ' Ollama has no server-side environment, agent or session management like Anthropic Managed Agents.
    ' Conversation history is therefore maintained locally and resent when useHistory is enabled.
    Private ReadOnly _httpClient As HttpClient
    Private ReadOnly _messages As New List(Of OllamaMessage)
    Private _lastResponse As String = String.Empty

    ' Creates the shared HTTP client used for all Ollama API requests.
    ' Local model calls may run for a long time, therefore a generous timeout is used.
    Public Sub New()
        _httpClient = New HttpClient()
        _httpClient.Timeout = TimeSpan.FromHours(2)
    End Sub

    ' Verifies that the configured Ollama endpoint is reachable and starts a fresh local session state.
    ' Conversation history, response state and accumulated run statistics are reset on connect.
    Protected Overrides Async Function ProviderConnectAsync(cancellationToken As CancellationToken) As Task
        cancellationToken.ThrowIfCancellationRequested()

        Dim baseUrl As String = GetOllamaBaseUrl().TrimEnd("/"c)
        Dim tagsUrl As String = baseUrl & "/api/tags"

        Using response As HttpResponseMessage = Await _httpClient.GetAsync(tagsUrl, cancellationToken)
            Dim responseText As String = Await response.Content.ReadAsStringAsync()
            If Not response.IsSuccessStatusCode Then
                Throw New Exception("OllamaProvider.ConnectAsync failed: " & response.StatusCode.ToString() & " - " & responseText)
            End If
        End Using

        _messages.Clear()
        _lastResponse = String.Empty
        currentSession.SessionId = "ollama-local-" & DateTime.Now.ToString("yyyyMMdd-HHmmss")
        currentSession.SessionStartTime = DateTime.Now
        currentSession.TokensIn = 0
        currentSession.TokensOut = 0
        currentSession.RateLimitWarningLevel = 0
        LogDebug("OllamaProvider: connected, session " & currentSession.SessionId)
    End Function

    ' Clears the local Ollama session state when the provider is disconnected.
    ' The Ollama server and loaded model are not stopped by this provider hook.
    Protected Overrides Function ProviderDisconnectAsync(cancellationToken As CancellationToken) As Task
        cancellationToken.ThrowIfCancellationRequested()
        currentSession.SessionId = String.Empty
        _messages.Clear()
        _lastResponse = String.Empty
        LogDebug("OllamaProvider: disconnected")
        Return Task.CompletedTask
    End Function

    ' Implements the common provider interface using the current Ollama runtime settings.
    ' clearHistory is mapped to the provider's local conversation history handling.
    Public Overrides Function SendPromptAsync(
    prompt As String,
    cancellationToken As CancellationToken,
    Optional clearHistory As Boolean = False,
    Optional inferenceSettings As ProviderInferenceSettings = Nothing) As Task
        Return Oll_SendPromptAsync(prompt:=prompt, cancellationToken:=cancellationToken, useHistory:=Not clearHistory, think:=currentSession.UseThinking, keepAlive:=currentSession.Keepalive)
    End Function

    ' Sends a chat request to Ollama and optionally maintains the conversation history locally.
    ' Failed history calls remove the unanswered user prompt so the stored conversation remains consistent.
    Public Async Function Oll_SendPromptAsync(prompt As String, cancellationToken As CancellationToken, Optional useHistory As Boolean = True, Optional think As Boolean = False, Optional keepAlive As String = "5m") As Task
        cancellationToken.ThrowIfCancellationRequested()

        If String.IsNullOrWhiteSpace(currentSession.SessionId) Then Throw New InvalidOperationException("OllamaProvider.Oll_SendPromptAsync: SessionId is missing. ConnectAsync was probably not called.")
        If String.IsNullOrWhiteSpace(prompt) Then Throw New ArgumentException("OllamaProvider.Oll_SendPromptAsync: Prompt is empty.", NameOf(prompt))
        If String.IsNullOrWhiteSpace(keepAlive) Then keepAlive = "5m"

        _lastResponse = String.Empty

        If bBiglogs Then LogDebug("<<<<<<<<<<<< OLLAMA RESPONSE" & vbCrLf & _lastResponse)

        Dim requestMessages As List(Of OllamaMessage)
        If useHistory Then
            _messages.Add(New OllamaMessage With {.role = "user", .content = prompt})
            requestMessages = New List(Of OllamaMessage)(_messages)
        Else
            _messages.Clear()
            requestMessages = New List(Of OllamaMessage) From {New OllamaMessage With {.role = "user", .content = prompt}}
        End If

        LogDebug("[Ollama SendPrompt] Prompt chars:" & prompt.Length.ToString() & " messages:" & requestMessages.Count.ToString() & " useHistory:" & useHistory.ToString() & " think:" & think.ToString() & " keep_alive:" & keepAlive)

        Dim requestBody As String = BuildChatRequestJson(requestMessages, think, keepAlive)

        Try
            Using content As New StringContent(requestBody, Encoding.UTF8, "application/json")
                Dim url As String = GetOllamaBaseUrl().TrimEnd("/"c) & "/api/chat"

                Using response As HttpResponseMessage = Await _httpClient.PostAsync(url, content, cancellationToken)
                    Dim responseText As String = Await response.Content.ReadAsStringAsync()
                    LogDebug("[Ollama SendPrompt] StatusCode: " & response.StatusCode.ToString())

                    If Not response.IsSuccessStatusCode Then Throw New HttpRequestException("Ollama Oll_SendPromptAsync failed: " & response.StatusCode.ToString() & " - " & responseText)

                    LogOllamaResponseStatistics(responseText)
                    _lastResponse = ParseChatResponse(responseText)

                    If String.IsNullOrWhiteSpace(_lastResponse) Then Throw New InvalidOperationException("OllamaProvider.Oll_SendPromptAsync: Response contains no text in message.content.")
                End Using
            End Using

            If useHistory Then _messages.Add(New OllamaMessage With {.role = "assistant", .content = _lastResponse})

        Catch
            If useHistory AndAlso _messages.Count > 0 AndAlso String.Equals(_messages(_messages.Count - 1).role, "user", StringComparison.OrdinalIgnoreCase) Then _messages.RemoveAt(_messages.Count - 1)
            Throw
        End Try
    End Function


    ' Extracts timing and token statistics from a completed Ollama response for diagnostic logging.
    ' Invalid or incomplete statistics never interrupt normal request processing.
    Private Sub LogOllamaResponseStatistics(ByVal responseText As String)
        If String.IsNullOrWhiteSpace(responseText) Then Exit Sub

        Try
            Using doc As JsonDocument = JsonDocument.Parse(responseText)
                Dim root As JsonElement = doc.RootElement
                Dim promptEvalCount As Long = 0
                Dim promptEvalDuration As Long = 0
                Dim evalCount As Long = 0
                Dim evalDuration As Long = 0
                Dim totalDuration As Long = 0
                Dim loadDuration As Long = 0
                Dim doneReason As String = ""
                Dim prop As JsonElement

                If root.TryGetProperty("prompt_eval_count", prop) Then promptEvalCount = prop.GetInt64()
                If root.TryGetProperty("prompt_eval_duration", prop) Then promptEvalDuration = prop.GetInt64()
                If root.TryGetProperty("eval_count", prop) Then evalCount = prop.GetInt64()
                If root.TryGetProperty("eval_duration", prop) Then evalDuration = prop.GetInt64()
                If root.TryGetProperty("total_duration", prop) Then totalDuration = prop.GetInt64()
                If root.TryGetProperty("load_duration", prop) Then loadDuration = prop.GetInt64()
                If root.TryGetProperty("done_reason", prop) Then doneReason = prop.GetString()

                Dim promptSeconds As Double = promptEvalDuration / 1000000000.0
                Dim evalSeconds As Double = evalDuration / 1000000000.0
                Dim totalSeconds As Double = totalDuration / 1000000000.0
                Dim loadSeconds As Double = loadDuration / 1000000000.0
                Dim promptTokensPerSecond As Double = 0
                Dim evalTokensPerSecond As Double = 0

                If promptSeconds > 0 Then promptTokensPerSecond = promptEvalCount / promptSeconds
                If evalSeconds > 0 Then evalTokensPerSecond = evalCount / evalSeconds

                LogDebug("[Ollama Statistics] done_reason=" & doneReason &
                         " prompt_tokens=" & promptEvalCount.ToString() & " prompt_time=" & promptSeconds.ToString("N2") & " s" & " prompt_speed=" & promptTokensPerSecond.ToString("N2") & " tok/s" &
                         " output_tokens=" & evalCount.ToString() & " output_time=" & evalSeconds.ToString("N2") & " s" & " output_speed=" & evalTokensPerSecond.ToString("N2") & " tok/s" &
                         " load_time=" & loadSeconds.ToString("N2") & " s" & " total_time=" & totalSeconds.ToString("N2") & " s")
            End Using
        Catch ex As Exception
            LogDebug("[ERROR] LogOllamaResponseStatistics: " & ex.Message)
        End Try
    End Sub

    ' Builds the final Ollama request using the mood and calculated token limits.
    ' Context and output budgets are derived from the actual messages and thinking mode.
    Private Function BuildChatRequestJson(messages As IReadOnlyCollection(Of OllamaMessage), think As Boolean, keepAlive As String) As String
        If messages Is Nothing OrElse messages.Count = 0 Then Throw New ArgumentException("No messages were provided for the Ollama request.", NameOf(messages))

        Dim moodSection As String = currentSession.AImood.ToString()
        Dim TopK As Integer = CInt(AgentsIni.ReadValue(moodSection, "TopK", "20"))
        Dim TopP As Double = Double.Parse(AgentsIni.ReadValue(moodSection, "TopP", "0.95"), CultureInfo.InvariantCulture)
        Dim MinP As Double = Double.Parse(AgentsIni.ReadValue(moodSection, "MinP", "0.05"), CultureInfo.InvariantCulture)
        Dim numPredict As Integer
        Dim contextSize As Integer = GetOllamaContextSizeForMessages(messages, think, numPredict)

        LogDebug("[Ollama StartPrompt] temperature:" & currentSession.temp & " top_p:" & TopP & " top_k:" & TopK & " min_p:" & MinP & " num_ctx:" & contextSize & " num_predict:" & numPredict & " seed:" & currentSession.seed & " think:" & think & " keep_alive:" & keepAlive)

        Dim options As New Dictionary(Of String, Object) From {
        {"temperature", currentSession.temp},
        {"top_p", TopP},
        {"top_k", TopK},
        {"min_p", MinP},
        {"num_ctx", contextSize},
        {"num_predict", numPredict},
        {"seed", currentSession.seed}
    }

        Dim request As New Dictionary(Of String, Object) From {
        {"model", GetOllamaModel()},
        {"messages", messages},
        {"stream", False},
        {"think", think},
        {"options", options},
        {"keep_alive", keepAlive}
    }

        Return JsonSerializer.Serialize(request)
    End Function

    ' Calculates the output budget and required context size for the current request.
    ' Thinking receives additional generation space before the final answer.
    Private Function GetOllamaContextSizeForMessages(messages As IEnumerable(Of OllamaMessage), think As Boolean, ByRef numPredict As Integer) As Integer
        Dim totalCharacters As Long = 0

        If messages IsNot Nothing Then
            For Each message As OllamaMessage In messages
                If message Is Nothing Then Continue For
                If message.content IsNot Nothing Then totalCharacters += message.content.Length
                totalCharacters += 32
            Next
        End If

        Dim estimatedInputTokens As Long = CLng(Math.Ceiling(totalCharacters / 2.5R))
        numPredict = If(think, 12048, 4096)
        Dim neededTokens As Long = estimatedInputTokens + numPredict
        LogDebug("[Ollama Context] Chars=" & totalCharacters.ToString() &
         " EstimatedInputTokens=" & estimatedInputTokens.ToString() &
         " NumPredict=" & numPredict.ToString() &
         " Needed=" & neededTokens.ToString())
        If neededTokens <= 8192L Then Return 8192
        If neededTokens <= 16384L Then Return 16384
        If neededTokens <= 24576L Then Return 24576
        If neededTokens <= 32768L Then Return 32768
        If neededTokens <= 40960L Then Return 40960
        If neededTokens <= 65536L Then Return 65536

        LogDebug("[Ollama Context] Estimated requirement exceeds 65536 tokens: " & neededTokens.ToString() & ". Context size capped at 65536.")
        Return 65536
    End Function

    ' Returns the response stored by the most recent Ollama request.
    ' The buffered response is consumed once and then cleared.
    Public Overrides Function ReceiveAsync(cancellationToken As CancellationToken) As Task(Of String)
        cancellationToken.ThrowIfCancellationRequested()
        If String.IsNullOrWhiteSpace(_lastResponse) Then Return Task.FromResult(Of String)(Nothing)

        Dim answer As String = _lastResponse
        _lastResponse = String.Empty
        Return Task.FromResult(answer)
    End Function
    ' Closes the current local Ollama session.
    ' Model unloading or process shutdown is handled separately by the application.
    Public Overrides Function CloseSessionAsync(cancellationToken As CancellationToken) As Task
        Return ProviderDisconnectAsync(cancellationToken)
    End Function

    ' Extracts the assistant text and token counts from a completed Ollama chat response.
    ' JSON escape sequences are already decoded by JsonDocument.
    Private Function ParseChatResponse(responseText As String) As String
        If String.IsNullOrWhiteSpace(responseText) Then Throw New ArgumentException("Ollama response is empty.", NameOf(responseText))

        Using doc As JsonDocument = JsonDocument.Parse(responseText)
            Dim root As JsonElement = doc.RootElement
            Dim answer As String = String.Empty
            Dim messageProperty As JsonElement

            If root.TryGetProperty("message", messageProperty) Then
                Dim contentProperty As JsonElement
                If messageProperty.TryGetProperty("content", contentProperty) Then
                    Dim rawContent As String = contentProperty.GetString()
                    If rawContent IsNot Nothing Then answer = rawContent
                End If
            End If

            Dim tokenProperty As JsonElement
            If root.TryGetProperty("prompt_eval_count", tokenProperty) Then currentSession.TokensIn += tokenProperty.GetInt32()
            If root.TryGetProperty("eval_count", tokenProperty) Then currentSession.TokensOut += tokenProperty.GetInt32()

            Return answer
        End Using
    End Function

    ' Returns the Ollama API base URL used by this provider.
    ' The default points to a standard local Ollama installation.
    Private Shared Function GetOllamaBaseUrl() As String
        Return "http://localhost:11434"
    End Function

    ' Returns the model selected for the current session.
    ' A Qwen model is used as fallback when no explicit model has been configured.
    Public Shared Function GetOllamaModel() As String
        If Not String.IsNullOrWhiteSpace(currentSession.Model) Then Return currentSession.Model.Trim()
        Return "qwen3:14b"
    End Function

    ' Represents one role/content message stored in the local Ollama conversation history.
    ' Property names intentionally match the JSON field names expected by Ollama.
    Private NotInheritable Class OllamaMessage
        Public Property role As String = String.Empty
        Public Property content As String = String.Empty
    End Class

End Class