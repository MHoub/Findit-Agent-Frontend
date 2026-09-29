Imports System.Threading

Public Class frm_Connect

    Private providerControls As New Dictionary(Of String, Control)
    Private trkExcerptContentLimit As TrackBar
    Private lblExcerptContentLimitValue As Label
    Private trkDuplicateSimilarityThreshold As TrackBar
    Private lblDuplicateSimilarityThresholdValue As Label
    Private bLoadingProviderValues As Boolean = False
    Private iNormalClientHeight As Integer
    Private bSuiteInstalled As Boolean = False
    Dim bModelCheck As Boolean = False
    Private sLastValidProvider As String = ""

    Private Shared Function SendMessage(hWnd As IntPtr, Msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Private Const WM_SETREDRAW As Integer = &HB

#Region "Form initialization"

    ' Initializes the connection dialog, colors, user information and provider list.
    ' Restores the last selected provider and triggers creation of its dynamic controls.
    Private Sub frm_Connect_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.Location = New Point(frmMain.Left + 100 + 300, frmMain.Top + 50)
        Me.Height = 755
        Me.Owner = frmMain
        ' Apply the current application colors.
        Me.BackColor = clr.Buttons
        txtUser.BackColor = clr.Background
        txtUser.ForeColor = clr.Text
        txtUserInfo.BackColor = clr.Background
        txtUserInfo.ForeColor = clr.Text
        bESC.BackColor = clr.Background
        btnOK.BackColor = clr.Background
        bESC.ForeColor = clr.Text
        btnOK.ForeColor = clr.Text
        rdbChunkExcerpts.ForeColor = clr.Text
        rdbAgent.ForeColor = clr.Text
        lblAGENTexplain.BackColor = BackColor
        lblAGENTexplain.ForeColor = clr.Text
        lblUsername.BackColor = Me.BackColor
        lblUsername.ForeColor = clr.Text
        lblUserInfo.BackColor = Me.BackColor
        lblUserInfo.ForeColor = clr.Text
        lblProvider.BackColor = Me.BackColor
        lblProvider.ForeColor = clr.Text
        ' Set fixed dialog lettering.
        Me.Text = "Connection with Model"
        lblProvider.Text = "Provider:"
        lblUsername.Text = "Name of User:"
        lblUserInfo.Text = "Basic information about User:"
        bESC.Text = "Cancel"
        btnOK.Text = "OK"
        ' Restore user information.
        txtUser.Text = AgentsIni.ReadValue("UserInfo", "UserName", "")
        txtUserInfo.Text = AgentsIni.ReadValue("UserInfo", "UserContext", "")

        Dim lastProvider As String = AgentsIni.ReadValue("Providers", "LastProvider")
        If lastProvider <> "" AndAlso cmbProvider.Items.Contains(lastProvider) Then
            cmbProvider.SelectedItem = lastProvider
        ElseIf cmbProvider.Items.Count > 0 Then
            cmbProvider.SelectedIndex = 0
        End If
        iNormalClientHeight = Me.ClientSize.Height
        Dim sRerankerPath As String = IO.Path.Combine(AppContext.BaseDirectory, "Reranker")
        bSuiteInstalled = IO.Directory.Exists(sRerankerPath)
        SetDarkTitleBar(Me)
        RestoreLastProviderSetting()
    End Sub

    Private Sub LoadProviderDropdown(sMode As String)

        cmbProvider.Items.Clear()

        Dim sIniKey As String

        If sMode.Equals("Agent", StringComparison.OrdinalIgnoreCase) Then
            sIniKey = "AgentProviders"
        Else
            sIniKey = "GuidedProviders"
        End If

        Dim providers As String = AgentsIni.ReadValue("Providers", sIniKey)

        For Each providerName As String In providers.Split(";"c)
            providerName = providerName.Trim()

            If providerName <> "" Then
                cmbProvider.Items.Add(providerName)
            End If
        Next

        Dim lastProvider As String = AgentsIni.ReadValue("Providers", "LastProvider")

        If lastProvider <> "" AndAlso cmbProvider.Items.Contains(lastProvider) Then
            cmbProvider.SelectedItem = lastProvider
        ElseIf cmbProvider.Items.Count > 0 Then
            cmbProvider.SelectedIndex = 0
        End If

    End Sub

#End Region

#Region "Provider controls and settings"

    ' Defines which editable controls are required for the selected provider.
    ' Add new provider-specific fields here when integrating another provider.
    ' Chunk Mode appends its two shared tuning controls automatically.
    Private Function GetProviderFields(providerName As String) As List(Of ProviderFieldDefinition)
        Dim fields As New List(Of ProviderFieldDefinition)
        Select Case providerName.Trim().ToLowerInvariant()
            Case "anthropic"
                fields.Add(New ProviderFieldDefinition With {.Key = "Model", .ControlType = "ComboBox", .Required = True})
                fields.Add(New ProviderFieldDefinition With {.Key = "EnvironmentName", .ControlType = "TextBox", .Required = True})
                fields.Add(New ProviderFieldDefinition With {.Key = "AgentName", .ControlType = "TextBox", .Required = True})
                fields.Add(New ProviderFieldDefinition With {.Key = "ApiKey", .ControlType = "Password", .Required = True})
            Case "openai"
                fields.Add(New ProviderFieldDefinition With {.Key = "Model", .ControlType = "ComboBox", .Required = True})
                fields.Add(New ProviderFieldDefinition With {.Key = "ApiKey", .ControlType = "Password", .Required = True})
            Case "ollama"
                fields.Add(New ProviderFieldDefinition With {.Key = "Model", .ControlType = "ComboBox", .Required = True})
                fields.Add(New ProviderFieldDefinition With {.Key = "BaseUrl", .ControlType = "TextBox", .Required = True})
                fields.Add(New ProviderFieldDefinition With {.Key = "AgentName", .ControlType = "TextBox", .Required = False})
            Case "mistral", "groq", "gemini", "perplexity", "scaleway", "stackit", "grok"
                fields.Add(New ProviderFieldDefinition With {.Key = "Model", .ControlType = "ComboBox", .Required = True})
                fields.Add(New ProviderFieldDefinition With {.Key = "BaseUrl", .ControlType = "TextBox", .Required = True})
                fields.Add(New ProviderFieldDefinition With {.Key = "ApiKey", .ControlType = "Password", .Required = True})
        End Select
        If rdbChunkExcerpts.Checked Then
            fields.Add(New ProviderFieldDefinition With {.Key = "ExcerptContentLimit", .ControlType = "TrackBar", .Required = False})
            fields.Add(New ProviderFieldDefinition With {.Key = "DuplicateSimilarityThreshold", .ControlType = "TrackBar", .Required = False})
        End If
        Return fields
    End Function

    ' Creates all provider-specific input controls dynamically.
    ' Chunk Mode trackbars are initialized with safe defaults and loaded separately.
    Private Sub BuildProviderPanel(providerName As String)

        pnlProviderData.Controls.Clear()
        providerControls.Clear()

        trkExcerptContentLimit = Nothing
        lblExcerptContentLimitValue = Nothing
        trkDuplicateSimilarityThreshold = Nothing
        lblDuplicateSimilarityThresholdValue = Nothing

        Dim y As Integer = 10
        Dim leftLabel As Integer = 15
        Dim leftControl As Integer = 15
        Dim fieldWidth As Integer = pnlProviderData.Width - 35

        Const labelHeight As Integer = 18
        Const labelControlGap As Integer = 3
        Const normalControlGap As Integer = 8
        Const trackBarBottomGap As Integer = 4
        Const trackBarVisualHeight As Integer = 22

        For Each field As ProviderFieldDefinition In GetProviderFields(providerName)

            Dim labelText As String

            Select Case field.Key
                Case "ExcerptContentLimit"
                    labelText = "Excerpt content limit"

                Case "DuplicateSimilarityThreshold"
                    labelText = "Duplicate similarity threshold"

                Case Else
                    labelText = LetteringIni.getLettering("Provider." & providerName, field.Key)
                    If String.IsNullOrWhiteSpace(labelText) Then labelText = field.Key
            End Select

            If field.Required Then labelText &= " *"

            Dim lbl As New Label With {
            .Name = "lbl" & field.Key,
            .Text = labelText,
            .Left = leftLabel,
            .Top = y,
            .Width = fieldWidth,
            .Height = labelHeight,
            .BackColor = Me.BackColor,
            .ForeColor = clr.Text
        }

            pnlProviderData.Controls.Add(lbl)

            Dim controlTop As Integer = lbl.Bottom + labelControlGap
            Dim ctl As Control = Nothing
            Dim rowBottom As Integer = controlTop

            Select Case field.ControlType

                Case "ComboBox"

                    Dim cmb As New ComboBox With {
                    .Name = "cmb" & field.Key,
                    .Left = leftControl,
                    .Top = controlTop,
                    .Width = fieldWidth,
                    .DropDownStyle = ComboBoxStyle.DropDownList
                }

                    If field.Key = "Model" Then
                        AddHandler cmb.SelectedIndexChanged, AddressOf cmbModel_SelectedIndexChanged
                    End If

                    ctl = cmb
                    rowBottom = ctl.Bottom

                Case "Password"

                    Dim txt As New TextBox With {
                    .Name = "txt" & field.Key,
                    .Left = leftControl,
                    .Top = controlTop,
                    .Width = fieldWidth,
                    .UseSystemPasswordChar = True
                }

                    ctl = txt
                    rowBottom = ctl.Bottom

                Case "TrackBar"

                    Dim valueLabelWidth As Integer = 90
                    Dim trackBarWidth As Integer = fieldWidth - valueLabelWidth - 5

                    If field.Key = "ExcerptContentLimit" Then

                        Dim storedValue As Integer = 40000

                        trkExcerptContentLimit = New TrackBar With {
    .Name = "trkExcerptContentLimit",
    .Left = leftControl,
    .Top = controlTop - 2,
    .Width = trackBarWidth,
    .AutoSize = False,
    .Height = 32,
    .Minimum = 25,
    .Maximum = 75,
    .Value = storedValue \ 1000,
    .TickFrequency = 5,
    .SmallChange = 1,
    .LargeChange = 5
}

                        lblExcerptContentLimitValue = New Label With {
                        .Name = "lblExcerptContentLimitValue",
                        .Left = leftControl + trackBarWidth + 5,
                        .Top = controlTop + 4,
                        .Width = valueLabelWidth,
                        .Height = 18,
                        .Text = (trkExcerptContentLimit.Value * 1000).ToString("N0"),
                        .BackColor = Me.BackColor,
                        .ForeColor = clr.Text
                    }

                        AddHandler trkExcerptContentLimit.ValueChanged,
                        Sub()
                            lblExcerptContentLimitValue.Text = (trkExcerptContentLimit.Value * 1000).ToString("N0")
                        End Sub

                        pnlProviderData.Controls.Add(lblExcerptContentLimitValue)

                        ctl = trkExcerptContentLimit
                        rowBottom = Math.Max(controlTop + trackBarVisualHeight, lblExcerptContentLimitValue.Bottom)

                    Else

                        Dim storedValue As Integer = 85

                        trkDuplicateSimilarityThreshold = New TrackBar With {
                        .Name = "trkDuplicateSimilarityThreshold",
                        .Left = leftControl,
                        .Top = controlTop - 2,
                        .Width = trackBarWidth,
                        .Minimum = 75,
                        .Maximum = 100,
                        .Value = storedValue,
                        .TickFrequency = 5,
                        .SmallChange = 1,
                        .LargeChange = 5,
                        .Height = 28
                    }

                        lblDuplicateSimilarityThresholdValue = New Label With {
                        .Name = "lblDuplicateSimilarityThresholdValue",
                        .Left = leftControl + trackBarWidth + 5,
                        .Top = controlTop + 4,
                        .Width = valueLabelWidth,
                        .Height = 18,
                        .Text = trkDuplicateSimilarityThreshold.Value.ToString() & " %",
                        .BackColor = Me.BackColor,
                        .ForeColor = clr.Text
                    }

                        AddHandler trkDuplicateSimilarityThreshold.ValueChanged,
                        Sub()
                            lblDuplicateSimilarityThresholdValue.Text = trkDuplicateSimilarityThreshold.Value.ToString() & " %"
                        End Sub

                        pnlProviderData.Controls.Add(lblDuplicateSimilarityThresholdValue)

                        ctl = trkDuplicateSimilarityThreshold
                        rowBottom = Math.Max(controlTop + trackBarVisualHeight, lblDuplicateSimilarityThresholdValue.Bottom)

                    End If

                Case Else

                    Dim txt As New TextBox With {
                    .Name = "txt" & field.Key,
                    .Left = leftControl,
                    .Top = controlTop,
                    .Width = fieldWidth,
                    .BorderStyle = BorderStyle.None
                }

                    ctl = txt
                    rowBottom = ctl.Bottom

            End Select

            pnlProviderData.Controls.Add(ctl)
            providerControls(field.Key) = ctl

            If field.ControlType = "TrackBar" Then
                y = rowBottom + trackBarBottomGap
            Else
                y = rowBottom + normalControlGap
            End If

        Next
        If lblDuplicateSimilarityThresholdValue IsNot Nothing Then
            lblDuplicateSimilarityThresholdValue.BringToFront()
        End If
        AdjustWindowHeight()
        Dim requiredHeight As Integer = y + 10

        If requiredHeight > pnlProviderData.Height Then
            pnlProviderData.Height = requiredHeight
        End If

        If rdbChunkExcerpts.Checked Then LoadChunkSettings()

    End Sub

    Private Sub cmbModel_SelectedIndexChanged(sender As Object, e As EventArgs)

        Debug.WriteLine("ModelChanged, Loading=" & bLoadingProviderValues.ToString())

        If bLoadingProviderValues Then Exit Sub

        Dim cmb As ComboBox = DirectCast(sender, ComboBox)
        If cmb.SelectedItem Is Nothing Then Exit Sub

    End Sub

    Private Sub RestoreLastProviderSetting()

        Dim sMode As String = AgentsIni.ReadValue("LastProviderSetting", "Mode").Trim()
        Dim sProvider As String = AgentsIni.ReadValue("LastProviderSetting", "Provider").Trim()
        Dim sModel As String = AgentsIni.ReadValue("LastProviderSetting", "Model").Trim()

        ' 1. Mode setzen
        If sMode.Equals("Guided", StringComparison.OrdinalIgnoreCase) Then
            rdbChunkExcerpts.Checked = True
            sMode = "Guided"
        Else
            rdbAgent.Checked = True
            sMode = "Agent"
        End If

        ' 2. Passende Provider laden
        LoadProviderDropdown(sMode)

        ' 3. Gespeicherten Provider wählen
        If sProvider <> "" AndAlso cmbProvider.Items.Contains(sProvider) Then
            cmbProvider.SelectedItem = sProvider
        ElseIf cmbProvider.Items.Count > 0 Then
            cmbProvider.SelectedIndex = 0
        End If

        ' 4. Gespeichertes Modell wählen
        If providerControls.ContainsKey("Model") Then

            Dim cmbModel As ComboBox = TryCast(providerControls("Model"), ComboBox)

            If cmbModel IsNot Nothing AndAlso
           sModel <> "" AndAlso
           cmbModel.Items.Contains(sModel) Then

                cmbModel.SelectedItem = sModel

            End If

        End If

    End Sub

    ' Loads the selected provider and chooses a suitable default research mode.
    ' Ollama is always treated as local and therefore defaults to Guided Mode.
    Private Sub cmbProvider_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbProvider.SelectedIndexChanged
        If cmbProvider.SelectedItem Is Nothing Then Exit Sub

        If cmbProvider.SelectedItem Is Nothing Then Exit Sub

        Dim sProvider As String = cmbProvider.SelectedItem.ToString().Trim()

        If sProvider = "----------------" Then
            If sLastValidProvider <> "" AndAlso cmbProvider.Items.Contains(sLastValidProvider) Then
                cmbProvider.SelectedItem = sLastValidProvider
            End If
            Exit Sub
        End If

        sLastValidProvider = sProvider

        Dim providerName As String = cmbProvider.Text.Trim()
        LoadProviderInternalValues(providerName)
        If providerName.Equals("Ollama", StringComparison.OrdinalIgnoreCase) Then providerValues("bLocal") = "True"
        Dim isLocal As Boolean = False
        Boolean.TryParse(providerValues("bLocal"), isLocal)
        BuildProviderPanel(providerName)
        LoadProviderValues(providerName)
    End Sub

    ' Loads editable provider settings from the INI into the generated controls.
    ' Model lists are restored here; stored environment and agent names become read-only.
    Private Sub LoadProviderValues(providerName As String)

        bLoadingProviderValues = True

        Try

            Dim sectionName As String = "Provider." & providerName

            For Each kvp In providerControls

                Dim key As String = kvp.Key
                Dim ctl As Control = kvp.Value
                Dim value As String = AgentsIni.ReadValue(sectionName, key)
                If TypeOf ctl Is TextBox Then
                    ' In this first version there is only one Environment/Agent per Provider
                    ' The user may define their name in the first sessen, if the are filled
                    ' they cant ba changed . Or user has do do the work manually within the
                    ' ini files and its providers routines to create Environments and agents"
                    Dim txt As TextBox = DirectCast(ctl, TextBox)
                    txt.Text = value
                    If key = "EnvironmentName" OrElse key = "AgentName" Then
                        txt.ReadOnly = Not String.IsNullOrWhiteSpace(value)
                    End If
                    ctl.BackColor = clr.Background
                    ctl.ForeColor = clr.Text
                ElseIf TypeOf ctl Is ComboBox Then
                    Dim cmb As ComboBox = DirectCast(ctl, ComboBox)
                    cmb.Items.Clear()
                    If key = "Model" Then
                        Dim models As String = AgentsIni.ReadValue(sectionName, "Models")
                        For Each modelName In models.Split(";"c)
                            If modelName.Trim() <> "" Then
                                cmb.Items.Add(modelName.Trim())
                            End If
                        Next
                        Dim lastModel As String = AgentsIni.ReadValue(sectionName, "LastModel")
                        If lastModel <> "" AndAlso cmb.Items.Contains(lastModel) Then
                            cmb.SelectedItem = lastModel
                        ElseIf cmb.Items.Count > 0 Then
                            cmb.SelectedIndex = 0
                        End If
                    Else
                        If value <> "" Then
                            cmb.Items.Add(value)
                            cmb.SelectedIndex = 0
                        End If
                        ctl.BackColor = clr.Buttons
                        ctl.ForeColor = clr.Text
                    End If
                End If


            Next

        Finally
            bLoadingProviderValues = False
        End Try

        If providerControls.ContainsKey("Model") Then
            Dim cmb As ComboBox = TryCast(providerControls("Model"), ComboBox)
        End If

    End Sub

    ' Loads provider settings that are needed for a session but are not edited here.
    ' Add additional internal provider keys to this list when a new provider requires them.
    ' Values are stored in providerValues and later mapped into SessionData.
    ' Loads provider settings that are needed internally but are not edited here.
    ' Add additional internal provider keys when a provider requires them.
    Private Sub LoadProviderInternalValues(providerName As String)

        providerValues.Clear()
        Dim sectionName As String = "Provider." & providerName
        providerValues("bLocal") = AgentsIni.ReadValue(sectionName, "IsLocal")
        Dim keys As String() = {
        "AgentId",
        "EnvironmentId",
        "BaseUrlAgents",
        "BaseUrlSessions",
        "BaseUrlEvents",
        "Models",
        "LastModel"
    }
        For Each key In keys
            providerValues(key) = AgentsIni.ReadValue(sectionName, key)
        Next

    End Sub

    ' Loads the stored Chunk Mode limits and applies them to the dynamic trackbars.
    ' Values are clamped to the ranges supported by the current UI controls.
    Private Sub LoadChunkSettings()
        Dim maxExcerptLength As Integer = 40000
        Dim duplicateThreshold As Integer = 85
        Integer.TryParse(AgentsIni.ReadValue("ChunkMode", "MaxExcerptLength", "40000"), maxExcerptLength)
        Integer.TryParse(AgentsIni.ReadValue("ChunkMode", "DublicateThreshold", "85"), duplicateThreshold)
        maxExcerptLength = Math.Max(25000, Math.Min(75000, maxExcerptLength))
        duplicateThreshold = Math.Max(75, Math.Min(100, duplicateThreshold))
        If trkExcerptContentLimit IsNot Nothing Then
            trkExcerptContentLimit.Value = maxExcerptLength \ 1000
            lblExcerptContentLimitValue.Text = maxExcerptLength.ToString("N0")
        End If
        If trkDuplicateSimilarityThreshold IsNot Nothing Then
            trkDuplicateSimilarityThreshold.Value = duplicateThreshold
            lblDuplicateSimilarityThresholdValue.Text = duplicateThreshold.ToString() & " %"
        End If
    End Sub

    ' Stores the current Chunk Mode limits when the corresponding controls exist.
    ' Agent Mode has no chunk trackbars, so this routine exits without changing settings.
    Private Sub SaveChunkSettings()
        If trkExcerptContentLimit Is Nothing OrElse trkDuplicateSimilarityThreshold Is Nothing Then Exit Sub
        AgentsIni.WriteValue("ChunkMode", "MaxExcerptLength", (trkExcerptContentLimit.Value * 1000).ToString())
        AgentsIni.WriteValue("ChunkMode", "DublicateThreshold", trkDuplicateSimilarityThreshold.Value.ToString())
    End Sub

#End Region

#Region "Session setup and connection"
    ' Saves the current settings, builds the session data and connects the provider.
    ' Provider selection is validated before creating the session workspace.
    Private Async Sub StoreANDconnect(sender As Object, e As EventArgs) Handles btnOK.Click
        If cmbProvider.SelectedItem Is Nothing OrElse String.IsNullOrWhiteSpace(cmbProvider.Text) Then
            MsgBoxNeu("Please choose provider.", MsgBoxStyle.OkOnly)
            Exit Sub
        End If

        frmMain.CreateNewSessionWorkspace()
        Me.Visible = False
        frmMain.Cursor = Cursors.WaitCursor

        Try
            AgentsIni.WriteValue("UserInfo", "UserName", txtUser.Text)
            AgentsIni.WriteValue("UserInfo", "UserContext", txtUserInfo.Text)
            SaveCurrentProviderSettings()
            SaveChunkSettings()

            Fill_CurrentSession()

            LogDebug("SessionData loaded:")
            LogDebug("Provider=" & currentSession.Provider)
            LogDebug("Model=" & currentSession.Model)
            If currentSession.EnvironmentName <> "" Then LogDebug("EnvironmentName=" & currentSession.EnvironmentName)
            If currentSession.EnvironmentId <> "" Then LogDebug("EnvironmentId=" & currentSession.EnvironmentId)
            LogDebug("AgentName=" & currentSession.AgentName)
            If currentSession.AgentId <> "" Then LogDebug("AgentId=" & currentSession.AgentId)
            LogDebug("Sessionmode=" & currentSession.Sessionmode)
            LogDebug("ChunkMode=" & currentSession.ChunkMode)
        Catch ex As Exception
            LogDebug("Problem storing/loading session settings: " & ex.Message)
            frmMain.Cursor = Cursors.Default
            Me.Visible = True
            MsgBoxNeu("Problem storing/loading session settings:" & vbCrLf & ex.Message, MsgBoxStyle.OkOnly)
            Exit Sub
        End Try
        Try
            ' Create and connect the selected provider.
            LogDebug("Provider: " & currentSession.Provider)
            currentProvider = c_ProviderFactory.CreateProvider()
            LogDebug("ConnectAsync wird gestartet")
            Await currentProvider.ConnectAsync(CancellationToken.None)
            LogDebug("ConnectAsync erfolgreich beendet")
            If currentSession.Provider = "Ollama" Then
                ' Warm up the local model with a short request.
                Await frmMain.Oll_SendPromptAsync("Bist Du bereit? Dann antworte nur mit JA!", useHistory:=False, think:=False, keepAlive:="5m")
                frmMain.btnConnect.ImageKey = "Local"
            Else
                frmMain.btnConnect.ImageKey = "Extern"
            End If
            frmMain.InitializeSendWorker()
            frmMain.InitializeListenWorker()
            frmMain.StartListenWorker()
            frmMain.btnConnect.Text = currentSession.Model
            If currentSession.Sessionmode = "CHUNK" Then
                frmMain.btnConnect.Text &= " Guided"
            Else
                frmMain.btnConnect.Text &= " Agent"
            End If
            frmMain.btnCurDir.Visible = True
            currentSession.bActive = True
        Catch ex As Exception
            LogDebug("Problem connecting: " & ex.Message)
            frmMain.Cursor = Cursors.Default
            Me.Visible = True
            MsgBoxNeu("Problem connecting:" & vbCrLf &
                     "No or wrong API-KEY?", MsgBoxStyle.OkOnly)
            Exit Sub
        End Try
        frmMain.Cursor = Cursors.Default
        frmMain.btnSend.Text = "start / send"
        If frmMain.txtPrompt.Text <> "" Then frmMain.btnSend.Enabled = True
        Dim sMode As String = If(rdbChunkExcerpts.Checked, "Guided", "Agent")
        Dim sProvider As String = cmbProvider.Text.Trim()
        Dim sModel As String = ""

        If providerControls.ContainsKey("Model") Then
            Dim cmbModel As ComboBox = TryCast(providerControls("Model"), ComboBox)
            If cmbModel IsNot Nothing Then sModel = cmbModel.Text.Trim()
        End If

        AgentsIni.WriteValue("LastProviderSetting", "Mode", sMode)
        AgentsIni.WriteValue("LastProviderSetting", "Provider", sProvider)
        AgentsIni.WriteValue("LastProviderSetting", "Model", sModel)

        Me.DialogResult = DialogResult.OK
        Me.Close()

    End Sub
    Private Sub FreezeWindow()
        SendMessage(Me.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero)
    End Sub

    Private Sub UnfreezeWindow()
        SendMessage(Me.Handle, WM_SETREDRAW, New IntPtr(1), IntPtr.Zero)
        Me.Refresh()
    End Sub

    Private Sub AdjustWindowHeight()

        ' Always start from the normal window size.
        Me.ClientSize = New Size(Me.ClientSize.Width, iNormalClientHeight)
        Me.PerformLayout()

        ' Find lowest dynamic provider control.
        Dim iProviderBottom As Integer = pnlProviderData.Top

        For Each ctl As Control In pnlProviderData.Controls
            iProviderBottom = Math.Max(iProviderBottom, pnlProviderData.Top + ctl.Bottom)
        Next

        ' Top position of the fixed user section.
        Dim iFooterTop As Integer = lblUsername.Top

        ' Required additional height, including a little spacing.
        Dim iExtra As Integer = Math.Max(0, iProviderBottom + 10 - iFooterTop)

        Me.ClientSize = New Size(Me.ClientSize.Width, iNormalClientHeight + iExtra)

    End Sub

    ' Stores editable settings of the currently selected provider.
    ' Only TextBox and ComboBox controls belong to the provider configuration.
    Private Sub SaveCurrentProviderSettings()
        Dim providerName As String = cmbProvider.Text.Trim()
        If providerName = "" Then Exit Sub

        Dim sectionName As String = "Provider." & providerName
        AgentsIni.WriteValue("Providers", "LastProvider", providerName)

        For Each kvp In providerControls
            Dim key As String = kvp.Key
            Dim ctl As Control = kvp.Value
            Dim value As String

            If TypeOf ctl Is TextBox Then
                value = DirectCast(ctl, TextBox).Text.Trim()
            ElseIf TypeOf ctl Is ComboBox Then
                value = DirectCast(ctl, ComboBox).Text.Trim()
            Else
                Continue For
            End If

            If key = "Model" Then
                AgentsIni.WriteValue(sectionName, "LastModel", value)
            Else
                AgentsIni.WriteValue(sectionName, key, value)
            End If
        Next
    End Sub

    ' Builds currentSession from user data, dynamic provider controls and internal INI values.
    ' Provider field keys are mapped by name to matching SessionData properties via reflection.
    ' New provider fields therefore work automatically when SessionData exposes the same key.
    Private Sub Fill_CurrentSession()
        currentSession = New SessionData()
        currentSession.Provider = cmbProvider.SelectedItem
        currentSession.UserName = txtUser.Text.Trim()
        currentSession.UserContext = txtUserInfo.Text.Trim()
        Dim maxExcerptLength As Integer = 40000
        Dim duplicateThreshold As Integer = 85
        Integer.TryParse(AgentsIni.ReadValue("ChunkMode", "MaxExcerptLength", "40000"), maxExcerptLength)
        Integer.TryParse(AgentsIni.ReadValue("ChunkMode", "DublicateThreshold", "85"), duplicateThreshold)
        If trkExcerptContentLimit IsNot Nothing Then maxExcerptLength = trkExcerptContentLimit.Value * 1000
        If trkDuplicateSimilarityThreshold IsNot Nothing Then duplicateThreshold = trkDuplicateSimilarityThreshold.Value
        currentSession.MaxExcerptLength = maxExcerptLength
        currentSession.DublicateThreshold = duplicateThreshold
        ' Transfer values from the dynamic provider controls.
        For Each kvp In providerControls
            Dim key As String = kvp.Key
            Dim ctrl As Control = kvp.Value
            Dim value As String = ""
            If TypeOf ctrl Is TextBox Then
                value = DirectCast(ctrl, TextBox).Text.Trim()
            ElseIf TypeOf ctrl Is ComboBox Then
                value = DirectCast(ctrl, ComboBox).Text.Trim()
            Else
                Continue For
            End If
            Dim prop = GetType(SessionData).GetProperty(key)
            If prop IsNot Nothing AndAlso prop.CanWrite Then
                prop.SetValue(currentSession, ConvertSessionValue(value, prop.PropertyType))
            End If
        Next
        ' Transfer internal provider values loaded from the INI.
        For Each kvp In providerValues
            Dim prop = GetType(SessionData).GetProperty(kvp.Key)
            If prop IsNot Nothing AndAlso prop.CanWrite Then
                prop.SetValue(currentSession, ConvertSessionValue(kvp.Value, prop.PropertyType))
            End If
        Next
        If rdbAgent.Checked Then
            currentSession.Sessionmode = "AGENT"
        Else
            currentSession.Sessionmode = "CHUNK"
        End If
        currentSession.SessionStartTime = DateTime.Now
        currentSession.ChunkMode = ""
        If rdbChunkExcerpts.Checked Then currentSession.ChunkMode = "EXCERPT"
    End Sub

    ' Converts string-based provider settings before assigning them to SessionData properties.
    ' Boolean properties are parsed explicitly; all other currently supported values stay strings.
    Private Function ConvertSessionValue(value As String, targetType As Type) As Object
        If targetType Is GetType(Boolean) Then
            Dim b As Boolean = False
            Boolean.TryParse(value, b)
            Return b
        End If
        Return value
    End Function

#End Region

#Region "Mode selection and form actions"

    ' Closes the connection dialog without saving or starting a provider session.
    ' No connection state is changed when the user cancels here.
    Private Sub bESC_Click(sender As Object, e As EventArgs) Handles bESC.Click
        If frmMain.btnConnect.ImageKey = "Connecting" Then frmMain.btnConnect.ImageKey = "OFF"
        frmMain.btnConnect.Text = "Connect LLM"
        Me.Close()
    End Sub

    ' Switches the dialog to Agent Mode and rebuilds the provider controls.
    ' The selected mode is transferred to currentSession only when the session is created.
    Private Sub rdbAgent_CheckedChanged(sender As Object, e As EventArgs) Handles rdbAgent.CheckedChanged
        FreezeWindow()
        If Not rdbAgent.Checked Then Exit Sub
        If bModelCheck = True Then Exit Sub
        bModelCheck = True
        LoadProviderDropdown("Agent")
        lblAGENTexplain.Text = "The AI will perform a complete search independently, start Findit, request some of the results for reading, and possibly start further searches until it has a result. This mode requires large, powerful models with a large context window. Locally, it has so far only been possible on very large workstations."
        If cmbProvider.SelectedItem IsNot Nothing Then
            Dim providerName As String = cmbProvider.Text.Trim()
            BuildProviderPanel(providerName)
            LoadProviderValues(providerName)
        End If
        bModelCheck = False
        btnOK.Enabled = True
        UnfreezeWindow()
    End Sub

    ' Switches the dialog to Guided Chunk Mode and rebuilds the provider controls.
    ' The selected mode is transferred to currentSession only when the session is created.
    Private Sub rdbChunkExcerpts_CheckedChanged(sender As Object, e As EventArgs) Handles rdbChunkExcerpts.CheckedChanged
        FreezeWindow()
        If Not rdbChunkExcerpts.Checked Then Exit Sub
        If bModelCheck = True Then Exit Sub
        bModelCheck = True
        LoadProviderDropdown("Guided")
        If bSuiteInstalled Then
            lblAGENTexplain.Text =
                "Local or smaller AI models are guided through the search process step by step. Findit6Agent uses local reranking to select the most relevant files, creates excerpts from them, and lets the AI produce its result from these excerpts. Currently the most stable and recommended mode for smaller or local AI models."

            btnOK.Enabled = True
        Else
            lblAGENTexplain.Text =
    "Guided mode is designed to help smaller or local AI models perform a complete search." & vbCrLf & vbCrLf &
    "To provide this functionality, you need the Findit6Agent Suite, which includes an additional reranker and the required Python tools."
            btnOK.Enabled = False
        End If
        If cmbProvider.SelectedItem IsNot Nothing Then
            Dim providerName As String = cmbProvider.Text.Trim()
            BuildProviderPanel(providerName)
            LoadProviderValues(providerName)
        End If
        bModelCheck = False
        UnfreezeWindow()
    End Sub

    Private Sub frm_Connect_FormClosed(sender As Object, e As FormClosedEventArgs) Handles Me.FormClosed
        If Not currentSession.bActive Then frmMain.btnConnect.Text = "Connect LLM"
    End Sub

#End Region

End Class
