Imports System.ComponentModel
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading

Public Class frmMain

    ' Shared state used by the main form and query workflow.
    ' The timer drives status updates while a query is running.
    Public myTimer As System.Windows.Forms.Timer
    Public startTime As DateTime
    Private bClosing As Boolean = False
    Private bFormShown As Boolean
    Public bStartForm As Boolean = True



    ' Sends a prompt through the active Ollama provider.
    ' Throws when the current provider is not an Ollama provider.
    Public Async Function Oll_SendPromptAsync(prompt As String, Optional useHistory As Boolean = True, Optional think As Boolean = False, Optional keepAlive As String = "5m") As Task
        Dim ollamaProvider As c_OllamaProvider = TryCast(currentProvider, c_OllamaProvider)

        If ollamaProvider Is Nothing Then
            Throw New InvalidOperationException("Der aktuell verbundene Provider ist kein Ollama-Provider.")
        End If

        Await ollamaProvider.Oll_SendPromptAsync(prompt, CancellationToken.None, useHistory, think, keepAlive)
    End Function

#Region "Form Setup"


    ' Initializes configuration paths, workspace folders and the main status timer.
    ' Provider-independent visual setup is deferred until the form is shown.
    Private Sub frmMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Visible = False
        ' Initialize the query/status timer.
        myTimer = New System.Windows.Forms.Timer
        myTimer.Interval = 1000
        myTimer.Enabled = False
        AddHandler myTimer.Tick, AddressOf MyTimer_Tick

        ' Findit6 user configuration is stored in AppData\Roaming\FINDIT6.
        Dim sRoamingPath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FINDIT6")
        Dim sFinditIni As String = Path.Combine(sRoamingPath, "findit6.ini")
        sPathgroupIni = Path.Combine(sRoamingPath, "Findit6_Pathgroups.ini")
        ' If Findit6 has never been started, let Findit6 create its own
        ' complete first-run configuration.
        If Not File.Exists(sFinditIni) Then
            ' Findit6Agent is installed in a subfolder of the Findit6 directory.
            Dim sStartupPath As String = Application.StartupPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            Dim sFinditPath As String = Directory.GetParent(sStartupPath).FullName
            Dim sFinditExe As String = Path.Combine(sFinditPath, "Findit6.exe")
            If Not File.Exists(sFinditExe) Then
                MsgBoxNeu("No functional Findit6 installation was found.")
                Application.Exit()
                Return
            End If
            Dim p As Process = Process.Start(
            New ProcessStartInfo With {
                .FileName = sFinditExe,
                .Arguments = "/initialize",
                .UseShellExecute = False,
                .CreateNoWindow = True
            })
            p.WaitForExit()
            If Not File.Exists(sFinditIni) Then
                MsgBoxNeu("Findit6 could not be initialized.")
                Application.Exit()
                Return
            End If
        End If
        ' Create the user-specific Findit6Agent.ini on first start.
        Dim sFinditAgentIni As String = Path.Combine(sRoamingPath, "Findit6Agent.ini")
        If Not File.Exists(sFinditAgentIni) Then
            Dim sDefaultAgentIni As String = Path.Combine(Application.StartupPath, "Findit6Agent.ini")
            If Not File.Exists(sDefaultAgentIni) Then
                MsgBoxNeu("Findit6Agent.ini could not be found.")
                Application.Exit()
                Return
            End If
            File.Copy(sDefaultAgentIni, sFinditAgentIni, False)
        End If
        PathgroupIni.SetPath(sPathgroupIni)
        AgentsIni.SetPath(sFinditAgentIni)
        LetteringIni.SetPath(Path.Combine(Application.StartupPath, "Findit6Agents_E.lan"))
        LoadAgentIni()
        ' Resolve the configured workspace.
        If String.IsNullOrWhiteSpace(AgentWorkspace) Then
            Dim sSource As String = Path.Combine(Application.StartupPath, "AgentsWorkSpace")
            AgentWorkspace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Findit6Agent", "AgentsWorkSpace")
            If Not Directory.Exists(AgentWorkspace) Then
                My.Computer.FileSystem.CopyDirectory(sSource, AgentWorkspace, False)
            End If
            AgentsIni.WriteValue("Main", "Agentworkspace", AgentWorkspace)
        Else
            'The folder does exist but maybee we need a backup-file
            RestoreWorkspaceFiles()
        End If
        ' Create required workspace folders on first start.
        If Not IO.Directory.Exists(AgentWorkspace) Then
            IO.Directory.CreateDirectory(AgentWorkspace)
        End If
        frmSplash.Show()
        If sUTFpath = "" Then
            sUTFpath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Findit6", "AgentCache")
        End If
        If Not IO.Directory.Exists(sUTFpath) Then
            IO.Directory.CreateDirectory(sUTFpath)
        End If
        DeleteOldSessionFolders()
        ' Prepare paths used by the agent workspace.
        ' Configure path-group and default FIJ storage.
        StandardFij.SetPath(Path.Combine(AgentWorkspace, "Standard.fij"))
        ' Visual formatting is applied after the form is shown.
    End Sub

    Private Sub RestoreWorkspaceFiles()
        Dim sSource As String = Path.Combine(AppContext.BaseDirectory, "AgentsWorkspace")
        For Each sFile As String In Directory.GetFiles(sSource)
            Dim sTarget As String = Path.Combine(AgentWorkspace, Path.GetFileName(sFile))
            If Not File.Exists(sTarget) Then
                File.Copy(sFile, sTarget)
            End If
        Next
    End Sub

    ' Applies the current UI theme and initializes visible form state.
    ' The form becomes visible only after its initial layout is prepared.
    Private Sub frmMain_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        ' Apply the current color scheme.
        Me.BackColor = clr.Background
        rtfbProtokoll.Cursor = Cursors.Arrow
        btnConnect.BackColor = clr.Buttons
        btnSend.BackColor = btnConnect.BackColor
        rtfbProtokoll.BackColor = clr.Buttons
        rtfbProtokoll.ForeColor = clr.Text
        For Each child As Control In Me.Controls
            child.ForeColor = clr.Text
        Next
        txtPrompt.BackColor = clr.Buttons
        Spinner.BackColor = rtfbProtokoll.BackColor
        txtPrompt.ForeColor = clr.Text
        txtWhere.BackColor = clr.Buttons
        txtWhere.ForeColor = clr.Text
        pnlWhere.BackColor = txtWhere.BackColor
        chkbOCR.ForeColor = clr.Text
        ' Some values from the AgentsIni
        chkbOCR.Checked = CBool(AgentsIni.ReadValue("Main", "OCR", "False"))
        iMaxStatExcerptKB = CInt(AgentsIni.ReadValue("Main", "iMaxStatExcerptKB", "400"))
        ' Reset status labels before the first connection.^^
        lblTokensUsed.Text = ""
        lblCurrentState.Visible = False
        txtWhere.Text = SelectedPathgroup.Name
        bFormShown = True
        frmMain_Resize(Me, EventArgs.Empty)
        SetDarkTitleBar(Me)
        Me.Visible = True
        bStartForm = False
    End Sub

    ' Repositions and resizes main controls when the window size changes.
    ' Layout updates are skipped until the form has completed its initial setup.
    Private Sub frmMain_Resize(sender As Object, e As EventArgs) Handles Me.Resize
        ' Keep the main controls aligned while the form is resized.
        If Not bFormShown Then Exit Sub
        rtfbProtokoll.Width = Me.Width - 50
        btnConnect.Left = rtfbProtokoll.Left + rtfbProtokoll.Width - btnConnect.Width - btnSettings.Width - 10
        btnWhere.Left = btnConnect.Left - btnWhere.Width - 10
        pnlWhere.Width = btnWhere.Left - pnlWhere.Left
        txtWhere.Width = pnlWhere.Width - 10
        btnSettings.Left = rtfbProtokoll.Left + rtfbProtokoll.Width - btnSettings.Width
        chkbOCR.Left = btnWhere.Left + btnWhere.Width - chkbOCR.Width
        rtfbProtokoll.Height = Me.Height - 370
        btnCurDir.Left = rtfbProtokoll.Left + rtfbProtokoll.Width - btnCurDir.Width
        btnCurDir.Top = rtfbProtokoll.Top + rtfbProtokoll.Height + 12
        lblTokensUsed.Top = rtfbProtokoll.Top + rtfbProtokoll.Height + 8
        lblTokensUsed.Left = rtfbProtokoll.Left + rtfbProtokoll.Width - (lblTokensUsed.Width + btnCurDir.Width + 10)
        lblPrompt.Top = lblTokensUsed.Top + 20
        txtPrompt.Top = lblPrompt.Top + 30
        txtPrompt.Width = rtfbProtokoll.Width
        btnSend.Top = txtPrompt.Top + txtPrompt.Height + 20
        btnSend.Left = txtPrompt.Left + txtPrompt.Width - btnSend.Width
        lblCurrentState.Top = btnSend.Top
        lblTokensUsed.Top = rtfbProtokoll.Top + rtfbProtokoll.Height + 8
        lblTokensUsed.Left = rtfbProtokoll.Left + rtfbProtokoll.Width - (lblTokensUsed.Width + btnCurDir.Width + 10)
    End Sub
    ' Releases provider, worker and timer resources before the application closes.
    ' Local Ollama sessions receive their dedicated process cleanup.
    ' Finishes provider and worker cleanup before the application is allowed to close.
    ' The first close request is paused until all asynchronous shutdown work has completed.
    Private Async Sub frmMain_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If bClosing Then Exit Sub

        e.Cancel = True
        bClosing = True
        Me.Hide()

        If currentSession.Sessionmode = "AGENT" Then
            SaveAgentProtocol()
        End If
        Try
            myTimer.Enabled = False
            StopListenWorker()

            If currentSession.Provider = "Ollama" Then
                If currentProvider IsNot Nothing Then
                    Await currentProvider.CloseSessionAsync(CancellationToken.None)
                End If

                Dim targets() As String = {"ollama", "ollama app", "ollama_llama_server", "llama-server"}

                For Each target As String In targets
                    For Each p As Process In Process.GetProcessesByName(target)
                        Try
                            p.Kill()
                            p.WaitForExit(3000)
                            LogDebug($"Process {target} (PID: {p.Id}) terminated.")
                        Catch ex As Exception
                            ' Ignore processes that have already terminated.
                        End Try
                    Next
                Next
            Else
                Await DisconnectCurrentSession()
            End If

            SaveAgentIni()



        Catch ex As Exception
            LogDebug("Problem while closing application: " & ex.Message)

        Finally
            If myTimer IsNot Nothing Then myTimer.Dispose()
        End Try

        Me.Close()
    End Sub

    ' Restarts the local Ollama runtime to begin with a clean process state.
    ' The routine also applies the environment settings used for local model cleanup.
    Public Sub ForceOllamaProcessRestart()
        ' Restart Ollama to begin with a clean local runtime.
        ' This is performed when the agent application starts.
        Try
            LogDebug("Bereinige VRAM: Beende bestehende Ollama-Prozesse...")

            ' Stop all currently running Ollama server and runner processes.
            ' These processes normally run in the same user context and require no elevation.

            Dim targets() As String = {"ollama", "ollama app", "ollama_llama_server", "llama-server"}

            For Each target As String In targets
                For Each p As Process In Process.GetProcessesByName(target)
                    Try
                        p.Kill()
                        p.WaitForExit(3000) ' Maximal 3 Sekunden pro Prozess warten
                        LogDebug($"Prozess {target} (PID: {p.Id}) erfolgreich beendet.")
                    Catch ex As Exception
                        ' Ignore processes that have already terminated.
                    End Try
                Next
            Next

            ' Give Windows a short moment to release the previous runtime resources.
            System.Threading.Thread.Sleep(1500)

            ' Resolve the Ollama executable path.
            ' Prefer the standard per-user installation path.
            Dim localAppData As String = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            Dim ollamaPath As String = Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe")

            ' Fall back to the system-wide installation path when necessary.

            If Not File.Exists(ollamaPath) Then
                ollamaPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe")
            End If

            If File.Exists(ollamaPath) Then
                LogDebug("Starte Ollama-Server frisch im Hintergrund...")

                Dim psi As New ProcessStartInfo()
                psi.FileName = ollamaPath
                psi.Arguments = "serve" ' Startet den API-Server im Hintergrund
                psi.UseShellExecute = False
                psi.CreateNoWindow = True ' Kein störendes Konsolenfenster für den Nutzer

                ' Configure the fresh process to release model resources quickly.
                ' Keep-alive is disabled so local model memory is not retained unnecessarily.
                ' Avoid retaining stale CUDA contexts after processing.
                psi.EnvironmentVariables("CUDA_DEVICE_WAITS_ON_ERROR") = "0"
                psi.EnvironmentVariables("OLLAMA_KEEP_ALIVE") = "0"

                Process.Start(psi)
                LogDebug("Ollama-Server läuft frisch mit optimiertem Keep-Alive-Cache.")
            Else
                LogDebug("FEHLER: ollama.exe konnte unter den Standardpfaden nicht gefunden werden!")
            End If

        Catch ex As Exception
            LogDebug("Kritischer Fehler beim Prozess-Neustart: " & ex.Message)
        End Try
    End Sub

#End Region

#Region "User Interaction"

    ' Connects through the provider dialog or disconnects the current session.
    ' The connect button therefore also acts as the disconnect control.
    Private Async Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
        If btnConnect.ImageKey = "Local" Or btnConnect.ImageKey = "Extern" Then
            myTimer.Enabled = False
            StopListenWorker()
            Await DisconnectCurrentSession()
            btnConnect.Text = "Connect LLM"
        Else
            btnConnect.ImageKey = "Connecting"
            btnConnect.Text = "Connecting LLM"
            frm_Connect.Show()
        End If
    End Sub

    ' Disconnects the active provider and resets session state.
    ' Connection-state UI is updated after the provider has been released.
    Private Async Function DisconnectCurrentSession() As Task
        ' Disconnects the active provider and resets session state.
        ' Connection-state UI is updated after the provider has been released.
        Try
            If currentProvider IsNot Nothing Then
                Await currentProvider.DisconnectAsync(CancellationToken.None)
            End If

            btnConnect.ImageKey = "OFF"
            currentSession = New SessionData()
            currentProvider = Nothing

            btnSend.Text = "awaiting connection"
            btnSend.Enabled = False
            btnSend.ForeColor = clr.Text

            LogDebug("Session disconnected.")
        Catch ex As Exception
            LogDebug("Disconnect error: " & ex.Message)
            MsgBoxNeu("Problem disconnecting" & vbCrLf & ex.Message, MsgBoxStyle.OkOnly)
        End Try
        btnConnect.Text = "Connect LLM"
    End Function

    ' Handles user prompts for both Guided Chunk Mode and Agent Mode.
    ' During an active Chunk search the same button also acts as Stop.
    Private Sub SendPromptButton(sender As Object, e As EventArgs) Handles btnSend.Click
        ' Send the current user prompt or stop an active Chunk search.


        If CurrentAction = ChunkActionEnum.WaitForNext Then
            ResetForNewQuery()
            CreateNewSessionWorkspace()
            rtfbProtokoll.ResetText()
        End If
        Dim sFullPrompt = ""
        Dim sRtfPrompt = txtPrompt.Text
        ' Process input only while a provider session is active.
        If currentSession.bActive Then
            sRtfPrompt = "[" & Now.ToString("G", CultureInfo.CurrentCulture) & "] " & txtPrompt.Text & vbCrLf
            ' Start or stop a Guided Chunk Mode query.
            If currentSession.Sessionmode = "CHUNK" Then
                If btnSend.Text = "STOP SEARCH" Then
                    Spinner.Visible = False
                    query.bStopQuerry = True
                    Exit Sub
                End If
                Spinner.Visible = True
                myTimer.Enabled = True
                startTime = Date.Now
                btnSend.Text = "STOP SEARCH"
                btnSend.Enabled = True
                btnSend.ForeColor = Color.LightPink
                LogDebug("----------->SendFIRSTprompt CHUNK")
                query = New Sessionquery()
                query.UserQuestion = txtPrompt.Text
                QueryPrompts("chunk_Query_0")
            Else
                If myTimer.Enabled = False Then
                    ' Prepare the first, small prompt that only determines the research mode.
                    query.UserQuestion = txtPrompt.Text.Trim.Replace(vbCrLf, "")
                    RenameAgentSessionFolder(query.UserQuestion)
                    sFullPrompt = BuildAgentModePrompt()
                    ' Abort if the research mode prompt could not be created.
                    If String.IsNullOrWhiteSpace(sFullPrompt) Then
                        LogDebug("----------->Agent Mode start aborted: mode prompt could not be created")
                        query.UserQuestion = ""
                        sFullPrompt = ""
                        Exit Sub
                    End If
                    LogDebug("----------->SendFIRSTprompt AGENT MODE DETECTION")
                    SendPromptAsync(sFullPrompt, True)
                    query.bAgentRunning = True
                    startTime = Date.Now
                    myTimer.Enabled = True
                Else

                    ' OpenAI-compatible providers cannot accept another user message
                    ' while the current Agent run is still active.
                    If TypeOf currentProvider Is OpenAICompatibleProvider AndAlso query.bAgentRunning Then
                        currentSession.sPendingUserPrompt = txtPrompt.Text
                        txtPrompt.Enabled = False
                        btnSend.Enabled = False
                        bFinditRunning = False
                        MsgBoxNeu("The provider you are using can process a new message only after the current Agent step has finished." & vbCrLf & vbCrLf &
                        "Please wait a moment. Your message has been queued and will be sent to the AI automatically as soon as the current step is complete.")
                        Exit Sub
                    End If
                    ' Send a normal Agent Mode follow-up prompt.
                    LogDebug("----------->SendUserPrompt")
                    sFullPrompt = txtPrompt.Text
                    sRtfPrompt = "[" & Date.Now.Subtract(startTime).ToString("mm\:ss") & "] " & txtPrompt.Text & vbCrLf & vbCrLf
                    SendPromptAsync(sFullPrompt)

                End If
            End If
        End If

        ' Append the user prompt to the visible protocol.
        With rtfbProtokoll
            .SelectionStart = .TextLength
            .SelectionLength = 0
            .SelectionColor = clr.UsrTxt
            .AppendText(sRtfPrompt & vbCrLf)
        End With
        rtfbProtokoll.Select(rtfbProtokoll.Text.Length, 0)
        rtfbProtokoll.ScrollToCaret()
        rtfbProtokoll.Update()
        txtPrompt.Text = ""

    End Sub

    Private Function BuildAgentModePrompt() As String
        Try
            If String.IsNullOrWhiteSpace(query.UserQuestion) Then Return ""

            Dim sPromptFile As String = Path.Combine(AgentWorkspace, "Agent_00_Start.md")
            If Not File.Exists(sPromptFile) Then
                LogDebug("----------->Agent_00_Start.md not found: " & sPromptFile)
                Return ""
            End If

            Dim sPrompt As String = File.ReadAllText(sPromptFile, Encoding.UTF8)
            sPrompt = sPrompt.Replace("%UserQuestion%", query.UserQuestion)

            Return sPrompt

        Catch ex As Exception
            LogDebug("----------->Problem building Agent Mode prompt: " & ex.Message)
            Return ""
        End Try
    End Function

    ' Renames the existing Agent Mode session folder using the initial user question.
    ' The timestamp is preserved and only a short file-system-safe label is appended.
    Private Sub RenameAgentSessionFolder(question As String)
        If String.IsNullOrWhiteSpace(SessionWorkspace) OrElse String.IsNullOrWhiteSpace(question) Then Exit Sub
        If Not Directory.Exists(SessionWorkspace) Then Exit Sub

        Dim label As String = question.Trim()

        ' Remove the first two whitespace-separated words.
        label = Regex.Replace(label, "^\s*\S+\s+\S+\s*", "").Trim()

        For Each c As Char In Path.GetInvalidFileNameChars()
            label = label.Replace(c, " "c)
        Next

        label = Regex.Replace(label, "\s+", "_").Trim("_"c, "."c)
        If label.Length > 50 Then label = label.Substring(0, 50).TrimEnd("_"c, "."c)
        If label = "" Then Exit Sub

        Dim newPath As String = SessionWorkspace & "_" & label
        If String.Equals(SessionWorkspace, newPath, StringComparison.OrdinalIgnoreCase) Then Exit Sub
        If Directory.Exists(newPath) Then Exit Sub

        Directory.Move(SessionWorkspace, newPath)
        SessionWorkspace = newPath
    End Sub

    ' Keeps the send button enabled only when input can currently be submitted.
    ' Stop and connection states take precedence over an empty prompt.
    Private Sub emptyTxtBox(sender As Object, e As EventArgs) Handles txtPrompt.TextChanged
        ' Enable the send button only when input can be submitted.
        If txtPrompt.Text = "" AndAlso btnSend.Text <> "STOP SEARCH" Then
            btnSend.Enabled = False
        ElseIf btnSend.Text <> "awaiting connection" Then
            btnSend.Enabled = True
        End If
    End Sub

    ' Opens the path-group dialog for selecting Findit6 search locations.
    ' The same action is available from the button, text field and surrounding panel.
    Private Sub txtWhere_Click(sender As Object, e As EventArgs) Handles btnWhere.Click, txtWhere.Click, pnlWhere.Click
        ' Open the path-group selection dialog.
        Cursor = Cursors.WaitCursor
        ' The dialog manages target paths grouped for Findit6 searches.
        frm_Gruppen.Show()
        ' Focus group creation when no path groups exist yet.
        If frm_Gruppen.cmbGruppen.Items.Count = 0 Then frm_Gruppen.cmbGruppen.Focus()
        Cursor = Cursors.Default
    End Sub

    ' Opens a file hyperlink clicked in the protocol window.
    ' File URI links are converted back to local paths before launching.
    Private Sub startclickedFile(sender As Object, e As LinkClickedEventArgs) Handles rtfbProtokoll.LinkClicked
        ' Open a file link selected in the protocol.
        Try
            Dim target As String = e.LinkText 'New Uri(e.LinkText).LocalPath
            If target.StartsWith("file:", StringComparison.OrdinalIgnoreCase) Then
                target = New Uri(target).LocalPath
            End If
            Process.Start(New ProcessStartInfo With {
            .FileName = target,
            .UseShellExecute = True
        })
        Catch ex As Exception
            MsgBoxNeu("Problem running file: " & ex.Message, MsgBoxStyle.OkOnly)
        End Try
    End Sub

    ' Opens the application settings dialog.
    Private Sub btnSettings_Click(sender As Object, e As EventArgs) Handles btnSettings.Click
        ' Open application settings.
        frm_Settings.Show()
    End Sub

    ' Synchronizes the OCR option with both Agent and Findit6 configuration.
    ' The related Findit6 file-type selection is updated at the same time.
    Private Sub chkbOCR_CheckedChanged(sender As Object, e As EventArgs) Handles chkbOCR.CheckedChanged
        If Not bStartForm And chkbOCR.Checked Then
            MsgBoxNeu("Attention: OCR can significantly slow down the FIRST search through your archive." & vbCrLf & vbCrLf &
              "During this first run, Findit6 builds an OCR cache. Once this cache exists, later OCR searches run at essentially the same speed as searches through normal files, with virtually no measurable additional delay." & vbCrLf & vbCrLf &
              "Tip: You can run Findit6 once directly, without the Agent, and let it perform an OCR search through your archive to build the cache in advance.")
        End If
        ' Synchronize the OCR option with the frontend and Findit6 configuration.
        ' Store the frontend preference.
        AgentsIni.WriteValue("Main", "OCR", chkbOCR.Checked.ToString)
        ' Store the corresponding Findit6 settings.
        StandardFij.WriteValue("What", "OCR", chkbOCR.Checked.ToString)
        StandardFij.WriteValue("What", "checkPicOCR", chkbOCR.Checked.ToString)
        ' Read the current file-type selection.
        Dim filetypestring As String = StandardFij.ReadValue("Filetypes", "Selection")
        ' Add or remove the OCR-related file-type selection.
        filetypestring = filetypestring.Replace("3", "")
        If chkbOCR.Checked Then filetypestring &= "3"
        StandardFij.WriteValue("Filetypes", "Selection", filetypestring)
    End Sub

    ' Opens the current session workspace in Windows Explorer.
    ' Missing or inaccessible workspaces are reported through MsgBoxNeu.
    Private Sub btnCurDir_Click(sender As Object, e As EventArgs) Handles btnCurDir.Click
        ' Open the current session workspace in Explorer.
        Try
            If Not IO.Directory.Exists(SessionWorkspace) Then
                MsgBoxNeu("Workspace not found:" & vbCrLf & SessionWorkspace,
                      MsgBoxStyle.OkOnly)
                Exit Sub
            End If
            Process.Start(New ProcessStartInfo With {.FileName = SessionWorkspace, .UseShellExecute = True})
        Catch ex As Exception
            MsgBoxNeu("Unable to open workspace:" & vbCrLf & ex.Message, MsgBoxStyle.OkOnly)
        End Try
    End Sub

#End Region

#Region "Process Control"

    ' Drives periodic UI updates while a query is active.
    ' It also advances the currently selected Chunk or Agent workflow.

    Private Sub MyTimer_Tick(sender As Object, e As EventArgs)
        Dim sMsgToUser As String = ""
        Dim hasRateLimitWarning As Boolean = currentSession.RateLimitWarningLevel > 0
        ' Reflect communication-worker state in the UI.
        If bworkerbusy Then
            lblCurrentState.Text = "Communication worker busy"
        Else
            lblCurrentState.Text = ""
        End If
        ' Temporarily block normal sending while a rate-limit warning is active.
        If hasRateLimitWarning Then
            If currentSession.RateLimitWarningLevel = 10 Then sMsgToUser = "Received Rate Limit warning"
            currentSession.RateLimitWarningLevel -= 1

            If lblCurrentState.Text <> "" Then lblCurrentState.Text &= " | "
            lblCurrentState.Text &= "Rate Limit warning received"
        End If
        ' Stop must always remain available during an active Chunk search.
        If btnSend.Text = "STOP SEARCH" Then
            btnSend.Enabled = True
        Else
            btnSend.Enabled = Not bworkerbusy AndAlso Not hasRateLimitWarning AndAlso txtPrompt.Text <> ""
        End If
        ' Continue the active workflow according to the selected session mode.
        Select Case currentSession.Sessionmode
            Case "CHUNK"
                sMsgToUser = ChunkModeRunner()

            Case "AGENT"
                If SErrMessage <> "" Then
                    sMsgToUser = sErrMessage
                Else
                    sMsgToUser = agentworkflow()
                End If
        End Select
        ' Append workflow messages to the visible protocol.
        If sMsgToUser <> "" Then UsrMsgToRtf(sMsgToUser)
        ' Update runtime and token usage.
        Dim elapsed As TimeSpan = DateTime.Now.Subtract(startTime)
        Dim elapsedString As String
        If elapsed.TotalHours >= 1 Then
            elapsedString = elapsed.ToString("h\:mm\:ss")
        Else
            elapsedString = elapsed.ToString("mm\:ss")
        End If
        lblTokensUsed.Text = "Time Running: " & elapsedString & "  | PromptBalance: " & iPromptBalance.ToString &
                         "  | Tokens In: " & currentSession.TokensIn.ToString & " | Out: " &
                         currentSession.TokensOut.ToString & " | TOTAL: " &
                         (currentSession.TokensIn + currentSession.TokensOut).ToString

        lblTokensUsed.Left = rtfbProtokoll.Left + rtfbProtokoll.Width - (lblTokensUsed.Width + btnCurDir.Width + 10)
        If sErrMessage <> "" Then
            sErrMessage = ""
            Spinner.Visible = False
        End If
    End Sub

    ' Appends an Agent file-reading message followed by a clickable file link.
    ' The visible message includes the elapsed query time.
    Public Sub AppendMessageWithLink(fileName As String)
        With rtfbProtokoll
            .SelectionStart = .TextLength
            .SelectionLength = 0
            .SelectionColor = clr.Text
            .AppendText("[" & DateTime.Now.Subtract(startTime).ToString("mm\:ss") & "] " & "Agent is reading: ")
        End With
        AppendFileLink(fileName)
        rtfbProtokoll.AppendText(vbCrLf)
    End Sub

    ' Inserts a local file path as an RTF hyperlink in the protocol.
    ' Link target and display text are escaped before being embedded in RTF.
    Private Sub AppendFileLink(fileName As String)
        Dim linkPath As String = RtfEscape(fileName.Replace("\", "/"))
        Dim displayText As String = RtfEscape(fileName)

        Dim rtfLink As String =
        "{\rtf1\ansi " &
        "{\field{\*\fldinst HYPERLINK ""file:///" & linkPath & """}" &
        "{\fldrslt " & displayText & "}}" &
        "\par}"

        Dim oldPos As Integer = rtfbProtokoll.SelectionStart
        rtfbProtokoll.Select(oldPos, 0)
        rtfbProtokoll.SelectedRtf = rtfLink
    End Sub

#Region "Result Output"

    ' Escapes plain text so it can safely be embedded in an RTF fragment.
    ' Non-ASCII characters are emitted using RTF Unicode escape sequences.
    Private Function RtfEscape(ByVal s As String) As String

        If s Is Nothing Then Return ""

        Dim sb As New System.Text.StringBuilder()

        For Each ch As Char In s
            Select Case ch
                Case "\"c
                    sb.Append("\\")
                Case "{"c
                    sb.Append("\{")
                Case "}"c
                    sb.Append("\}")
                Case Else
                    Dim code As Integer = AscW(ch)

                    If code > 127 Then
                        If code > 32767 Then code -= 65536
                        sb.Append("\u")
                        sb.Append(code.ToString(Globalization.CultureInfo.InvariantCulture))
                        sb.Append("?")
                    Else
                        sb.Append(ch)
                    End If
            End Select
        Next

        Return sb.ToString()

    End Function

#End Region

#End Region

#Region "RTF Handling"

    ' Replaces the last protocol line with updated text.
    ' Cross-thread calls are forwarded to the UI thread before formatting.
    Public Sub UpdateLastProtocolLine(ByVal newLine As String)
        If Me.InvokeRequired Then
            Me.Invoke(New Action(Of String)(AddressOf UpdateLastProtocolLine), newLine)
            Exit Sub
        End If
        If newLine Is Nothing Then newLine = ""
        Dim elapsed As TimeSpan = DateTime.Now.Subtract(startTime)
        If elapsed.TotalHours >= 1 Then
            newLine = "[" & elapsed.ToString("h\:mm\:ss") & "] " & newLine
        Else
            newLine = "[" & elapsed.ToString("mm\:ss") & "] " & newLine
        End If
        With rtfbProtokoll
            Dim text As String = .Text
            If text.Length = 0 Then
                .AppendText(newLine & vbCrLf)
                .SelectionStart = .TextLength
                .SelectionLength = 0
                .ScrollToCaret()
                LogDebug("UpdateLastProtocolLine APPEND FIRST: " & newLine)
                Exit Sub
            End If
            Dim endPos As Integer = text.Length
            Do While endPos > 0 AndAlso (text.Chars(endPos - 1) = ControlChars.Cr OrElse text.Chars(endPos - 1) = ControlChars.Lf)
                endPos -= 1
            Loop
            If endPos <= 0 Then
                .Clear()
                .AppendText(newLine & vbCrLf)
                .SelectionStart = .TextLength
                .SelectionLength = 0
                .ScrollToCaret()
                LogDebug("UpdateLastProtocolLine REPLACE EMPTY: " & newLine)
                Exit Sub
            End If
            Dim startPos As Integer = text.LastIndexOf(ControlChars.Lf, endPos - 1)
            If startPos < 0 Then
                startPos = 0
            Else
                startPos += 1
            End If
            .SelectionStart = startPos
            .SelectionLength = endPos - startPos
            .SelectedText = newLine
            .SelectionStart = .TextLength
            .SelectionLength = 0
            .Refresh()
        End With
    End Sub

    ' Appends a timestamped workflow message to the protocol.
    ' Optional file-link markers are converted into clickable RTF hyperlinks.
    Public Sub UsrMsgToRtf(ByVal sMsgToUser As String)

        Dim elapsed As TimeSpan = DateTime.Now.Subtract(startTime)

        If bContextLimit Then sMsgToUser = CONTEXT_LIMIT_MESSAGE

        If elapsed.TotalHours >= 1 Then
            sMsgToUser = "[" & elapsed.ToString("h\:mm\:ss") & "] " & sMsgToUser
        Else
            sMsgToUser = "[" & elapsed.ToString("mm\:ss") & "] " & sMsgToUser
        End If

        With rtfbProtokoll
            .SelectionStart = .TextLength
            .SelectionLength = 0
            .SelectionColor = clr.Text

            Dim linkStart As Integer = sMsgToUser.IndexOf("<|FILELINK|>", StringComparison.Ordinal)

            If linkStart < 0 Then
                .AppendText(sMsgToUser & vbCrLf & vbCrLf)
            Else
                Dim displayStart As Integer = sMsgToUser.IndexOf("<|DISPLAY|>", linkStart, StringComparison.Ordinal)
                Dim linkEnd As Integer = sMsgToUser.IndexOf("<|ENDLINK|>", displayStart, StringComparison.Ordinal)

                If displayStart < 0 OrElse linkEnd < 0 Then
                    .AppendText(sMsgToUser & vbCrLf & vbCrLf)
                Else
                    Dim textBefore As String = sMsgToUser.Substring(0, linkStart)
                    Dim linkUrl As String = sMsgToUser.Substring(linkStart + "<|FILELINK|>".Length, displayStart - linkStart - "<|FILELINK|>".Length)
                    Dim displayText As String = sMsgToUser.Substring(displayStart + "<|DISPLAY|>".Length, linkEnd - displayStart - "<|DISPLAY|>".Length)
                    Dim textAfter As String = sMsgToUser.Substring(linkEnd + "<|ENDLINK|>".Length)

                    .AppendText(textBefore)

                    Dim rtfLink As String =
                "{\rtf1\ansi " &
                "{\field{\*\fldinst HYPERLINK """ & RtfEscape(linkUrl) & """}" &
                "{\fldrslt " & RtfEscape(displayText) & "}}" &
                "}"

                    .SelectionStart = .TextLength
                    .SelectionLength = 0
                    .SelectedRtf = rtfLink
                    .AppendText(textAfter & vbCrLf & vbCrLf)
                End If
            End If
        End With
        If bContextLimit Then
            myTimer.Stop()
            DisconnectCurrentSession()
            LogDebug("------- Maximum session limit of provider was reached. Session was stopped.")
        End If
    End Sub

#End Region

    ' Returns the vertical position of the end of the RichTextBox content.
    Private Function GetTextEndTop(rtb As RichTextBox) As Integer
        If rtb.TextLength = 0 Then Return 0

        Dim endPosition As Point = rtb.GetPositionFromCharIndex(rtb.TextLength)
        Return endPosition.Y
    End Function

    ' Positions the activity spinner near the end of the visible protocol text.
    ' The spinner is hidden when that position falls outside the visible area.
    Private Sub UpdateTextEndTop()
        If currentSession.bActive Then
            Spinner.Top = rtfbProtokoll.Top + GetTextEndTop(rtfbProtokoll) - 20
            If Spinner.Top + Spinner.Height > rtfbProtokoll.Top + rtfbProtokoll.Height Then
                Spinner.Visible = False
            Else
                If CurrentAction <> ChunkActionEnum.FinishedALL And CurrentAction <> ChunkActionEnum.Idle Then
                    Spinner.Visible = True
                End If
            End If
        End If
    End Sub

    ' Repositions the activity spinner after protocol content changes.
    Private Sub RichTextBox1_TextChanged(sender As Object, e As EventArgs) Handles rtfbProtokoll.TextChanged
        If currentSession.bActive Then
            rtfbProtokoll.BeginInvoke(New Action(AddressOf UpdateTextEndTop))
        End If
    End Sub

    ' Repositions the activity spinner when the protocol is scrolled.
    Private Sub RichTextBox1_VScroll(sender As Object, e As EventArgs) Handles rtfbProtokoll.VScroll
        UpdateTextEndTop()
    End Sub

    ' Repositions the activity spinner when the protocol control is resized.
    Private Sub RichTextBox1_Resize(sender As Object, e As EventArgs) Handles rtfbProtokoll.Resize
        If currentSession.bActive Then
            rtfbProtokoll.BeginInvoke(New Action(AddressOf UpdateTextEndTop))
        End If
    End Sub

    ' Deletes session folders older than the configured retention period.
    ' Individual deletion failures are logged without stopping the cleanup pass.
    Private Sub DeleteOldSessionFolders()
        Dim deletionWeeks As Integer = CInt(AgentsIni.ReadValue("Main", "WeeksToKeepSessionFolder", "2"))
        If deletionWeeks <= 0 Then Exit Sub
        If Not Directory.Exists(AgentWorkspace) Then Exit Sub

        Dim deleteBefore As DateTime = DateTime.Now.AddDays(-(deletionWeeks * 7))

        For Each sessionDir As DirectoryInfo In New DirectoryInfo(AgentWorkspace).GetDirectories()
            Try
                If sessionDir.LastWriteTime < deleteBefore Then
                    LogDebug(">>>> Deleting old session folder: " & sessionDir.FullName)
                    sessionDir.Delete(True)
                End If
            Catch ex As Exception
                LogDebug(">>>> Could not delete old session folder: " & sessionDir.FullName & " | " & ex.Message)
            End Try
        Next
    End Sub

    ' Creates a timestamped workspace folder for a new query session.
    Public Sub CreateNewSessionWorkspace()
        Dim sessionFolder As String = DateTime.Now.ToString("yyyyMMdd_HHmmss")
        SessionWorkspace = IO.Path.Combine(AgentWorkspace, sessionFolder)
        IO.Directory.CreateDirectory(SessionWorkspace)
    End Sub

    Private Sub lblTokensUsed_Click(sender As Object, e As EventArgs) Handles lblTokensUsed.Click

    End Sub
End Class