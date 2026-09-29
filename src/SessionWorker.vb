Imports System.ComponentModel
Imports System.Text
Imports System.Text.Json
Imports System.Threading

Partial Public Class frmMain

    Private bgWorkerListen As New BackgroundWorker
    Private ReadOnly listenCancellationLock As New Object()
    Private listenReceiveCts As CancellationTokenSource = Nothing
    Public bRecoveryGracePeriod As Boolean = False
    Public dtRecoveryResponse As DateTime = DateTime.MinValue

    ' Initializes the background listener that receives provider responses.
    ' The worker remains reusable and supports explicit cancellation.
    Public Sub InitializeListenWorker()
        bgWorkerListen.WorkerReportsProgress = False
        bgWorkerListen.WorkerSupportsCancellation = True
        AddHandler bgWorkerListen.DoWork, AddressOf bgWorkerListen_DoWork
        AddHandler bgWorkerListen.RunWorkerCompleted, AddressOf bgWorkerListen_RunWorkerCompleted
        LogDebug("ListenWorker initialized")
    End Sub

    ' Starts the listener unless it is already running.
    ' Existing listener instances are reused between runs.
    Public Sub StartListenWorker()
        If bgWorkerListen.IsBusy Then
            LogDebug(">>> ListenWorker already running")
            Exit Sub
        End If

        LogDebug(">>> StartListenWorker: starting worker")
        bgWorkerListen.RunWorkerAsync()
    End Sub

    ' Requests cancellation of both the BackgroundWorker and the active receive call.
    ' A finished receive may already have disposed its CancellationTokenSource.
    Public Sub StopListenWorker()
        If Not bgWorkerListen.IsBusy Then Return

        LogDebug(">>> StopListenWorker: requesting cancellation")
        bgWorkerListen.CancelAsync()

        Dim activeCts As CancellationTokenSource = Nothing
        SyncLock listenCancellationLock
            activeCts = listenReceiveCts
        End SyncLock

        If activeCts IsNot Nothing Then
            Try
                activeCts.Cancel()
            Catch ex As ObjectDisposedException
                ' The receive operation has already finished.
            End Try
        End If
    End Sub

    ' Continuously waits for provider responses until cancellation is requested.
    ' Individual receive calls use a timeout and are retried after temporary failures.
    Private Sub bgWorkerListen_DoWork(sender As Object, e As DoWorkEventArgs)


        Dim iterationCount As Integer = 0

        Try
            While Not bgWorkerListen.CancellationPending
                If Not String.IsNullOrWhiteSpace(KIresponse) Then
                    Thread.Sleep(50)
                    Continue While
                End If
                iterationCount += 1

                Try
                    Dim iReceiveTimeout As Integer = 120


                    If TypeOf currentProvider Is OpenAICompatibleProvider OrElse currentSession.Provider.Equals("Perplexity", StringComparison.OrdinalIgnoreCase) Then
                        iReceiveTimeout = 3600
                    End If

                    Dim cts As New CancellationTokenSource(TimeSpan.FromSeconds(iReceiveTimeout))

                    SyncLock listenCancellationLock
                        listenReceiveCts = cts
                        If bgWorkerListen.CancellationPending Then cts.Cancel()
                    End SyncLock

                    Try
                        Dim response As String = currentProvider.ReceiveAsync(cts.Token).GetAwaiter().GetResult()

                        ReleaseSendCancellation()

                        If String.IsNullOrWhiteSpace(response) Then
                            Thread.Sleep(100)
                            Continue While
                        End If

                        OnResponseReceived(response)
                        LogDebug(">>> ListenWorker: response received, length: " & response.Length.ToString())

                    Finally
                        SyncLock listenCancellationLock
                            If Object.ReferenceEquals(listenReceiveCts, cts) Then listenReceiveCts = Nothing
                        End SyncLock

                        cts.Dispose()
                    End Try

                Catch ex As OperationCanceledException

                    If bgWorkerListen.CancellationPending Then
                        LogDebug(">>> ListenWorker intentionally cancelled")
                        Exit While
                    End If

                    If CurrentAction = ChunkActionEnum.ExcerptCANCELLED Then
                        ReleaseSendCancellation()
                        CurrentAction = ChunkActionEnum.ExcerptRESTART
                        LogDebug("######### >> Excerpt request cancelled after timeout; restart requested")
                    Else
                        LogDebug(">>> ListenWorker timeout - retry")
                        Thread.Sleep(1000)
                    End If

                Catch ex As Exception

                    LogDebug("[ERROR] ListenWorker: " & ex.Message)

                    Dim sUserError As String = BuildProviderErrorMessage(ex.Message)
                    sErrMessage = sUserError


                    Thread.Sleep(1000)

                End Try
            End While

            e.Result = New With {.success = True, .iterations = iterationCount}

        Catch ex As Exception
            e.Result = New With {.success = False, .iterations = iterationCount, .message = ex.Message}
        End Try
    End Sub

    ' Repairs raw line breaks that are illegal inside JSON string values.
    ' Regular line breaks outside JSON strings are preserved unchanged.
    Private Function SanitizeRawJsonLines(ByVal rawText As String) As String
        If String.IsNullOrEmpty(rawText) Then Return rawText

        Dim result As New StringBuilder(rawText.Length)
        Dim insideString As Boolean = False
        Dim escaped As Boolean = False
        Dim i As Integer = 0

        Do While i < rawText.Length
            Dim currentChar As Char = rawText.Chars(i)

            If insideString Then
                If escaped Then
                    result.Append(currentChar)
                    escaped = False
                    i += 1
                    Continue Do
                End If

                If currentChar = "\"c Then
                    result.Append(currentChar)
                    escaped = True
                    i += 1
                    Continue Do
                End If

                If currentChar = """"c Then
                    result.Append(currentChar)
                    insideString = False
                    i += 1
                    Continue Do
                End If

                If currentChar = ControlChars.Cr Then
                    result.Append("\n")
                    If i + 1 < rawText.Length AndAlso rawText.Chars(i + 1) = ControlChars.Lf Then
                        i += 2
                    Else
                        i += 1
                    End If
                    Continue Do
                End If

                If currentChar = ControlChars.Lf Then
                    result.Append("\n")
                    i += 1
                    Continue Do
                End If

                If AscW(currentChar) = &H2028 OrElse AscW(currentChar) = &H2029 Then
                    result.Append(" "c)
                    i += 1
                    Continue Do
                End If

                result.Append(currentChar)
                i += 1
                Continue Do
            End If

            If currentChar = """"c Then insideString = True
            result.Append(currentChar)
            i += 1
        Loop

        Return result.ToString()
    End Function

    ' Updates prompt accounting and forwards a received provider response to the workflow.
    ' Invalid JSON is preserved so the existing recovery logic can request a corrected reply.
    Private Sub OnResponseReceived(response As String)

        If String.IsNullOrWhiteSpace(response) Then
            LogDebug("Empty response ignored")
            Exit Sub
        End If

        Dim cleanResponse As String = response.Trim()

        If cleanResponse.Equals("JA", StringComparison.OrdinalIgnoreCase) OrElse cleanResponse.Equals("JA!", StringComparison.OrdinalIgnoreCase) Then
            LogDebug("<<<< Warmup response ignored: " & cleanResponse)
            Return
        End If

        dtLastAgentResponse = Now

        Dim balance As Integer = Threading.Volatile.Read(iPromptBalance)

        If bTimeoutRecovery Then

            If bReminderPending Then
                sPromptToProcess = ""
                bReminderPending = False
                LogDebug("<<<< Late response arrived before timeout reminder was sent - reminder cancelled")
            End If

            If TryDecrementPromptBalance(balance) Then
                LogDebug("<<<< Timeout recovery response received, PromptBalance = " & balance.ToString())
            Else
                balance = 0
                LogDebug("<<<< Timeout recovery response received while PromptBalance is already 0")
            End If

            bTimeoutRecovery = False

            If balance > 0 Then
                bRecoveryGracePeriod = True
                dtRecoveryResponse = Now
                LogDebug("<<<< Recovery succeeded, but " & balance.ToString() & " earlier response(s) are still outstanding")
            Else
                bRecoveryGracePeriod = False
                dtRecoveryResponse = DateTime.MinValue
            End If

        Else

            If TryDecrementPromptBalance(balance) Then
                LogDebug("<<<< Response received, PromptBalance = " & balance.ToString())
            Else
                balance = 0
                LogDebug("<<<< Additional response received while PromptBalance is already 0")
            End If

            ' If all previously outstanding responses have now arrived,
            ' the recovery grace period is no longer needed.
            If bRecoveryGracePeriod AndAlso balance = 0 Then
                bRecoveryGracePeriod = False
                dtRecoveryResponse = DateTime.MinValue
                LogDebug("<<<< All outstanding responses received - recovery grace period ended")
            End If

        End If

        LogDebug(" <<<<<<<<<<<<<<< ResponseReceived")
        If bBiglogs Then LogDebug("<<<<<<<<<<<<<< AI RESPONSE" & vbCrLf & response)
        Dim sanitizedResponse As String = SanitizeRawJsonLines(response)
        Dim validJson As String = ""

        If Not TryExtractValidJson(sanitizedResponse, validJson) Then
            LogDebug("No valid JSON found")
            LogDebug("Invalid JSON: " & sanitizedResponse)
            bInvalidJson = True
            ' Keep the invalid response so AgentRunner can request a corrected JSON reply.
            KIresponse = sanitizedResponse
            Exit Sub
        End If

        LogDebug("Response contains valid JSON")
        KIresponse = validJson

    End Sub

    ' Decrements the prompt balance only while it is greater than zero.
    ' Returns False when no outstanding prompt was available.
    Public Function TryDecrementPromptBalance(ByRef newBalance As Integer) As Boolean
        Do
            Dim currentBalance As Integer = Threading.Interlocked.CompareExchange(iPromptBalance, 0, 0)

            If currentBalance <= 0 Then
                newBalance = 0
                Return False
            End If

            newBalance = currentBalance - 1
            If Threading.Interlocked.CompareExchange(iPromptBalance, newBalance, currentBalance) = currentBalance Then Return True
        Loop
    End Function

    ' Records that the background listener has stopped.
    ' Worker lifetime is otherwise managed by the reusable listener instance.
    Private Sub bgWorkerListen_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs)
        LogDebug(">>> ListenWorker stopped")
    End Sub

    ' Requests listener cancellation and waits until the worker has stopped.
    ' The short delay keeps the caller responsive while the receive operation exits.
    Public Async Function StopListenWorkerAndWaitAsync() As Task
        StopListenWorker()
        Do While bgWorkerListen.IsBusy
            Await Task.Delay(50)
        Loop
    End Function

    ' Stops the listener, creates a new Anthropic working session and restarts listening.
    ' The provider itself remains connected while the remote working session is replaced.

    Public Async Function RestartAnthropicSessionAsync() As Task
        LogDebug(">>> RestartAnthropicSessionAsync: stopping listener")
        Await StopListenWorkerAndWaitAsync()
        LogDebug(">>> RestartAnthropicSessionAsync: listener confirmed stopped")

        Dim anthropicProvider As AnthropicProvider = TryCast(currentProvider, AnthropicProvider)
        If anthropicProvider Is Nothing Then Throw New InvalidOperationException("The current provider is not an Anthropic provider.")

        Await anthropicProvider.StartNewWorkingSessionAsync(CancellationToken.None)

        LogDebug(">>> RestartAnthropicSessionAsync: new session ready")
        StartListenWorker()
        LogDebug(">>> RestartAnthropicSessionAsync: listener restarted")
    End Function

    Private Function BuildProviderErrorMessage(ByVal sError As String) As String

        Const sHeader As String = "The AI provider returned an error:"

        If String.IsNullOrWhiteSpace(sError) Then
            Return sHeader & vbCrLf & vbCrLf & "Unknown error."
        End If

        Dim sMessage As String = ""

        ' A provider may return pure JSON, or explanatory text followed by JSON.
        ' Try every possible JSON start until one parses successfully.
        For i As Integer = 0 To sError.Length - 1

            If sError(i) <> "{"c AndAlso sError(i) <> "["c Then Continue For

            Try
                Using oJson As JsonDocument = JsonDocument.Parse(sError.Substring(i))
                    sMessage = FindProviderErrorMessage(oJson.RootElement)
                    If Not String.IsNullOrWhiteSpace(sMessage) Then Exit For
                End Using
            Catch
                ' Not valid JSON at this position - simply try the next one.
            End Try

        Next

        If String.IsNullOrWhiteSpace(sMessage) Then sMessage = sError.Trim()

        Return sHeader & vbCrLf & vbCrLf & sMessage.Trim()

    End Function


    Private Function FindProviderErrorMessage(ByVal oElement As JsonElement) As String

        Select Case oElement.ValueKind

            Case JsonValueKind.Object

                ' Prefer typical human-readable error fields.
                For Each sName As String In {"message", "error_description", "detail"}

                    For Each oProperty As JsonProperty In oElement.EnumerateObject()

                        If oProperty.Name.Equals(sName, StringComparison.OrdinalIgnoreCase) Then

                            If oProperty.Value.ValueKind = JsonValueKind.String Then
                                Return oProperty.Value.GetString()
                            End If

                        End If

                    Next

                Next

                ' Some providers return "error" directly as a string.
                For Each oProperty As JsonProperty In oElement.EnumerateObject()

                    If oProperty.Name.Equals("error", StringComparison.OrdinalIgnoreCase) AndAlso
                       oProperty.Value.ValueKind = JsonValueKind.String Then

                        Return oProperty.Value.GetString()

                    End If

                Next

                ' Otherwise search recursively.
                For Each oProperty As JsonProperty In oElement.EnumerateObject()

                    Dim sFound As String = FindProviderErrorMessage(oProperty.Value)
                    If Not String.IsNullOrWhiteSpace(sFound) Then Return sFound

                Next

            Case JsonValueKind.Array

                For Each oItem As JsonElement In oElement.EnumerateArray()

                    Dim sFound As String = FindProviderErrorMessage(oItem)
                    If Not String.IsNullOrWhiteSpace(sFound) Then Return sFound

                Next

            Case JsonValueKind.String

                Return oElement.GetString()

        End Select

        Return ""

    End Function


End Class
