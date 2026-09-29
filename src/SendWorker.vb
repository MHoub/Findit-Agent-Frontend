Option Explicit On
Option Strict On

Imports System.ComponentModel
Imports System.Threading

Partial Public Class frmMain

    Private ReadOnly sendCancellationSync As New Object()
    Public sendCancellation As CancellationTokenSource
    Public bgWorkerSend As BackgroundWorker = Nothing

    ' Initializes the per-request send worker infrastructure.
    ' Each prompt is sent by its own short-lived BackgroundWorker instance.
    Public Sub InitializeSendWorker()
        LogDebug("SendWorker initialized: per-request mode")
    End Sub

    ' Starts a new prompt request and cancels any request that is still running.
    ' Each worker owns its CancellationTokenSource and remains responsible for its own cleanup.
    Public Sub SendPromptAsync(userPrompt As String,
                           Optional isFirstRequest As Boolean = False,
                           Optional clearHistory As Boolean = False,
                           Optional useGuidedSettings As Boolean = False)

        Dim requestCancellation As New CancellationTokenSource()

        If bBiglogs Then LogDebug(">>>>>>>>>>>>>>>  PROMPT: " & userPrompt)

        SyncLock sendCancellationSync
            If sendCancellation IsNot Nothing Then
                Try
                    sendCancellation.Cancel()
                Catch ex As ObjectDisposedException
                End Try
            End If

            sendCancellation = requestCancellation
        End SyncLock

        If bgWorkerSend IsNot Nothing Then
            LogDebug(">>> SendPromptAsync: previous SendWorker is being cancelled")
        End If

        bgWorkerSend = New BackgroundWorker() With {
        .WorkerReportsProgress = False,
        .WorkerSupportsCancellation = False
    }

        AddHandler bgWorkerSend.DoWork, AddressOf bgWorkerSend_DoWork
        AddHandler bgWorkerSend.RunWorkerCompleted, AddressOf bgWorkerSend_RunWorkerCompleted

        Dim inferenceSettings As ProviderInferenceSettings = Nothing

        If useGuidedSettings Then
            inferenceSettings = New ProviderInferenceSettings With {
            .Mood = currentSession.AImood,
            .Think = currentSession.UseThinking,
            .Temperature = currentSession.temp
        }
        End If

        Dim args = New With {
        .userPrompt = userPrompt,
        .isFirstRequest = isFirstRequest,
        .clearHistory = clearHistory,
        .cancellationSource = requestCancellation,
        .inferenceSettings = inferenceSettings
    }

        LogDebug(">>> SendPromptAsync: Starting NEW SendWorker")
        bgWorkerSend.RunWorkerAsync(args)

    End Sub

    ' Sends the requested prompt through the currently connected provider.
    ' The worker disposes only the CancellationTokenSource belonging to its own request.
    Private Sub bgWorkerSend_DoWork(sender As Object, e As DoWorkEventArgs)
        Dim requestCancellation As CancellationTokenSource = Nothing
        Try
            Dim args As Object = CType(e.Argument, Object)
            Dim userPrompt As String = CStr(GetPropertyValue(args, "userPrompt"))
            Dim isFirstRequest As Boolean = CBool(GetPropertyValue(args, "isFirstRequest"))
            Dim clearHistory As Boolean = CBool(GetPropertyValue(args, "clearHistory"))
            Dim inferenceSettings As ProviderInferenceSettings = TryCast(GetPropertyValue(args, "inferenceSettings"), ProviderInferenceSettings)
            requestCancellation = CType(GetPropertyValue(args, "cancellationSource"), CancellationTokenSource)
            Dim cancellationToken As CancellationToken = requestCancellation.Token
            If currentProvider Is Nothing Then Throw New Exception("No AI provider is available.")
            If Not currentProvider.IsConnected Then Throw New Exception("AI provider is not connected.")
            If String.IsNullOrWhiteSpace(userPrompt) Then Throw New Exception("Prompt is empty.")
            Dim iAttempt As Integer = 0
            Const iMaxAttempts As Integer = 3
            Do
                iAttempt += 1
                Try
                    currentProvider.SendPromptAsync(userPrompt, cancellationToken, clearHistory, inferenceSettings).GetAwaiter().GetResult()
                    Exit Do
                Catch ex As OperationCanceledException
                    Throw
                Catch ex As Exception
                    Dim sMsg As String = ex.Message
                    Dim bTemporary As Boolean =
                    sMsg.IndexOf("overloaded", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                    sMsg.IndexOf("try again later", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                    sMsg.IndexOf("ServiceUnavailable", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                    sMsg.IndexOf("BadGateway", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                    sMsg.IndexOf("GatewayTimeout", StringComparison.OrdinalIgnoreCase) >= 0
                    If Not bTemporary OrElse iAttempt >= iMaxAttempts Then Throw
                    Dim iDelay As Integer = If(iAttempt = 1, 3000, 7000)
                    LogDebug(">>> Temporary provider error. Retry " & (iAttempt + 1).ToString() &
                         "/" & iMaxAttempts.ToString() & " in " & (iDelay \ 1000).ToString() &
                         " s: " & ex.Message)
                    Task.Delay(iDelay, cancellationToken).GetAwaiter().GetResult()
                End Try
            Loop
            LogDebug(">>> SendWorker: SendPromptAsync successful")
            Threading.Interlocked.Increment(iPromptBalance)
            dtLastPromptSent = Now
            bReminderPending = False
            LogDebug(">>> SendWorker: incremented iPromptBalance +1 = " & iPromptBalance.ToString())
            e.Result = New With {.success = True, .message = "POST OK"}
        Catch ex As OperationCanceledException
            LogDebug(">>> SendWorker: SendPromptAsync cancelled")
            e.Result = New With {.success = False, .message = "CANCELLED"}
        Catch ex As Exception
            LogDebug("[ERROR] SendWorker.DoWork: " & ex.Message)
            e.Result = New With {.success = False, .message = ex.Message}
        End Try
    End Sub

    Private Sub ReleaseSendCancellation()

        Dim oldCancellation As CancellationTokenSource = Nothing

        SyncLock sendCancellationSync
            oldCancellation = sendCancellation
            sendCancellation = Nothing
        End SyncLock

        If oldCancellation IsNot Nothing Then
            Try
                oldCancellation.Dispose()
            Catch ex As ObjectDisposedException
            End Try
        End If

    End Sub

    ' Processes the result of a completed SendWorker and disposes that worker.
    ' Only the currently active worker may change the global chunk workflow state.
    Private Sub bgWorkerSend_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs)
        Dim w As BackgroundWorker = TryCast(sender, BackgroundWorker)
        Dim isCurrentWorker As Boolean = Object.ReferenceEquals(bgWorkerSend, w)

        LogDebug(">>> ENTER bgWorkerSend_RunWorkerCompleted")

        Try
            If e.Error IsNot Nothing Then
                LogDebug("[ERROR] SendWorker error: " & e.Error.Message)

                If isCurrentWorker Then
                    query.StopReason = "AI provider error: " & e.Error.Message
                    query.bStopQuerry = True
                End If

                Exit Sub
            End If

            If e.Result Is Nothing Then
                LogDebug("[ERROR] SendWorker returned no result")

                If isCurrentWorker Then
                    query.StopReason = "AI provider error: no response was returned."
                    query.bStopQuerry = True
                End If

                Exit Sub
            End If

            Dim result As Object = CType(e.Result, Object)
            Dim success As Boolean = CBool(GetPropertyValue(result, "success"))
            Dim message As String = CStr(GetPropertyValue(result, "message"))

            If success Then
                LogDebug(">>> SendWorker completed successfully")

            Else
                LogDebug(">>> SendWorker failed: " & message)

                If String.Equals(message, "CANCELLED", StringComparison.OrdinalIgnoreCase) AndAlso
               CurrentAction = ChunkActionEnum.ExcerptCANCELLED Then

                    CurrentAction = ChunkActionEnum.ExcerptRESTART
                    LogDebug("######### >> Excerpt worker ended after timeout cancellation; restart requested")

                ElseIf CurrentAction = ChunkActionEnum.ExcerptsRUN Then

                    CurrentAction = ChunkActionEnum.ExcerptRESTART
                    LogDebug("[WARN] Excerpt request failed; continuing with the next ranked file.")

                ElseIf isCurrentWorker AndAlso
                   Not String.Equals(message, "CANCELLED", StringComparison.OrdinalIgnoreCase) Then

                    query.StopReason = "AI provider error: " & message
                    query.bStopQuerry = True
                    LogDebug("[ERROR] Query will be stopped because the AI request failed.")
                End If
            End If

        Catch ex As Exception
            LogDebug("[ERROR] SendWorker.RunWorkerCompleted: " & ex.Message)

        Finally
            If w IsNot Nothing Then
                Try
                    RemoveHandler w.DoWork, AddressOf bgWorkerSend_DoWork
                    RemoveHandler w.RunWorkerCompleted, AddressOf bgWorkerSend_RunWorkerCompleted
                    w.Dispose()
                Catch ex As Exception
                    LogDebug("[ERROR] disposing SendWorker: " & ex.Message)
                End Try
            End If

            If Object.ReferenceEquals(bgWorkerSend, w) Then bgWorkerSend = Nothing
        End Try
    End Sub

End Class