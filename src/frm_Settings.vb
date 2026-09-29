Imports System.IO

Public Class frm_Settings

#Region "Form initialization and settings"

    ' Initializes position, colors and current settings when the dialog opens.
    ' The workspace path and session retention value are loaded into their controls.
    Private Sub frm_Settings_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Top = frmMain.Top + 10
        Me.Left = frmMain.Left + (frmMain.Width - Me.Width) / 2
        Me.BackColor = clr.Background
        TxtbxWorkspace.BackColor = clr.Buttons
        TxtbxWorkspace.ForeColor = clr.Text
        TxtbxWorkspace.Text = AgentWorkspace
        For Each ctrl As Control In Me.Controls
            If TypeOf ctrl Is Button Then
                ctrl.BackColor = clr.Buttons
                ctrl.ForeColor = clr.Text
            ElseIf TypeOf ctrl Is Label Then
                ctrl.ForeColor = Color.White
            End If
        Next
        chbBigLogs.ForeColor = Color.White
        numKeepSession.Value = CInt(AgentsIni.ReadValue("Main", "WeeksToKeepSessionFolder", "2"))
        Me.Owner = frmMain
        SetDarkTitleBar(Me)
    End Sub

    ' Saves persistent settings and closes the dialog.
    ' Big logging remains a temporary runtime option and is intentionally not stored here.
    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        AgentsIni.WriteValue("Main", "WeeksToKeepSessionFolder", numKeepSession.Value.ToString)
        AgentsIni.WriteValue("Main", "AgentWorkspace", AgentWorkspace)
        Me.Close()
    End Sub

    ' Enables or disables extended prompt and response logging for the current runtime.
    ' This option is intentionally not persisted between application starts.
    Private Sub chbBigLogs_CheckedChanged(sender As Object, e As EventArgs) Handles chbBigLogs.CheckedChanged
        bBiglogs = chbBigLogs.Checked
    End Sub

#End Region

#Region "Workspace management"

    ' Lets the user select a new Agent workspace and creates the standard workspace folder if required.
    ' Existing editable workspace files are copied to the new location without overwriting target files.
    Private Sub btnWorkspace_Click(sender As Object, e As EventArgs) Handles btnWorkspace.Click
        Dim oldWorkspace As String = TxtbxWorkspace.Text.Trim()
        Using dlg As New FolderBrowserDialog
            dlg.Description = "Select Agent Workspace"
            If IO.Directory.Exists(TxtbxWorkspace.Text.Trim) Then dlg.SelectedPath = TxtbxWorkspace.Text.Trim
            Try
                If dlg.ShowDialog(Me) = DialogResult.OK Then
                    If String.IsNullOrWhiteSpace(dlg.SelectedPath) Then Exit Sub
                    Dim p As String = dlg.SelectedPath.Trim().TrimEnd("\"c, "/"c)
                    Dim lastFolder As String = IO.Path.GetFileName(p)
                    If lastFolder.Equals("AgentsWorkSpace", StringComparison.OrdinalIgnoreCase) Then
                        AgentWorkspace = p
                    Else
                        AgentWorkspace = IO.Path.Combine(p, "AgentsWorkSpace")
                    End If
                    If Not IO.Directory.Exists(AgentWorkspace) Then IO.Directory.CreateDirectory(AgentWorkspace)
                    CopyWorkspaceBaseFiles(oldWorkspace, AgentWorkspace)
                End If
            Catch ex As Exception
                AgentWorkspace = oldWorkspace
                MsgBoxNeu("Could not change Agent Workspace:" & vbCrLf & ex.Message, MsgBoxStyle.OkOnly)
            End Try
            TxtbxWorkspace.Text = AgentWorkspace
        End Using
    End Sub

    ' Copies editable workspace files to a newly selected workspace.
    ' Existing target files are preserved and optional files are ignored if missing.
    Private Sub CopyWorkspaceBaseFiles(sourceWorkspace As String, targetWorkspace As String)
        If String.IsNullOrWhiteSpace(sourceWorkspace) Then Exit Sub
        If String.IsNullOrWhiteSpace(targetWorkspace) Then Exit Sub
        If Not IO.Directory.Exists(sourceWorkspace) Then Exit Sub
        If Not IO.Directory.Exists(targetWorkspace) Then IO.Directory.CreateDirectory(targetWorkspace)

        Dim filesToCopy As String() = {
            "Agent_01_Briefing.md",
            "Agent_02_Syntax.md",
            "Agent_03_Communication.md",
            "Agent_04_Rules.md",
            "chunk_Query_UserRules.md"
        }

        For Each fileName As String In filesToCopy
            Dim sourceFile As String = IO.Path.Combine(sourceWorkspace, fileName)
            Dim targetFile As String = IO.Path.Combine(targetWorkspace, fileName)
            If IO.File.Exists(sourceFile) AndAlso Not IO.File.Exists(targetFile) Then IO.File.Copy(sourceFile, targetFile, overwrite:=False)
        Next
    End Sub

    ' Opens the current Agent workspace in Windows Explorer.
    ' A message is shown if the configured workspace is not available.
    Private Sub btnFolder_Click(sender As Object, e As EventArgs) Handles btnFolder.Click
        Try
            If Not IO.Directory.Exists(AgentWorkspace) Then
                MsgBoxNeu("Workspace not found:" & vbCrLf & AgentWorkspace, MsgBoxStyle.OkOnly)
                Exit Sub
            End If
            Process.Start(New ProcessStartInfo With {.FileName = AgentWorkspace, .UseShellExecute = True})
        Catch ex As Exception
            MsgBoxNeu("Unable to open workspace:" & vbCrLf & ex.Message, MsgBoxStyle.OkOnly)
        End Try
    End Sub

#End Region

#Region "Configuration and help files"

    ' Opens the main Findit6Agent INI file with the associated system application.
    ' The file is expected in the application working directory.
    Private Sub btnINI_Click(sender As Object, e As EventArgs) Handles btnINI.Click
        Try
            Dim sRoamingPath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FINDIT6")
            Dim filePath = Path.Combine(sRoamingPath, "Findit6Agent.ini")
            If Not IO.File.Exists(filePath) Then
                MsgBoxNeu("File not found:" & vbCrLf & filePath, MsgBoxStyle.OkOnly)
                Exit Sub
            End If
            Process.Start(New ProcessStartInfo With {.FileName = filePath, .UseShellExecute = True})
        Catch ex As Exception
            MsgBoxNeu("Unable to open file:" & vbCrLf & ex.Message, MsgBoxStyle.OkOnly)
        End Try
    End Sub

    ' Opens the Guided Mode documentation from the current Agent workspace.
    Private Sub btnGuided_Click(sender As Object, e As EventArgs) Handles btnGuided.Click
        OpenFile("Findit6Agent_GuidedMode.html")
    End Sub

    ' Opens the Agent Mode documentation from the current Agent workspace.
    Private Sub btnAgent_Click(sender As Object, e As EventArgs) Handles btnAgent.Click
        OpenFile("Findit6Agent_AgentMode.html")
    End Sub

    ' Opens the Agent briefing documentation from the current Agent workspace.

    ' Opens a file located in the current Agent workspace with its associated application.
    ' Missing files and shell launch errors are reported to the user.
    Private Sub OpenFile(fileName As String)
        Try
            Dim filePath As String = IO.Path.Combine(AgentWorkspace, fileName)
            If Not IO.File.Exists(filePath) Then
                MsgBoxNeu("File not found:" & vbCrLf & filePath, MsgBoxStyle.OkOnly)
                Exit Sub
            End If
            Process.Start(New ProcessStartInfo With {.FileName = filePath, .UseShellExecute = True})
        Catch ex As Exception
            MsgBoxNeu("Unable to open file:" & vbCrLf & ex.Message, MsgBoxStyle.OkOnly)
        End Try
    End Sub

    Private Sub btnFinditProfessional_Click(sender As Object, e As EventArgs) Handles btnFinditProfessional.Click
        Process.Start(New ProcessStartInfo("https://www.dateisuche.de/en/index.html") With {.UseShellExecute = True})
    End Sub

    Private Sub btnGithub_Click_1(sender As Object, e As EventArgs) Handles btnGithub.Click
        Process.Start(New ProcessStartInfo("https://github.com/MHoub/Findit-Agent-Frontend") With {.UseShellExecute = True})
    End Sub

    Private Sub btnReadFirst_Click(sender As Object, e As EventArgs) Handles btnReadFirst.Click
        OpenFile("FinditAgent_Start.html")
    End Sub

    Private Sub Button1Models_Click(sender As Object, e As EventArgs) Handles btnModels.Click
        OpenFile("Findit6Agent_Model_Behavior.html")
    End Sub

    Private Sub Label4_Click(sender As Object, e As EventArgs) Handles Label4.Click

    End Sub

#End Region

End Class