<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frm_Connect
    Inherits System.Windows.Forms.Form

    'Das Formular überschreibt den Löschvorgang, um die Komponentenliste zu bereinigen.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Wird vom Windows Form-Designer benötigt.
    Private components As System.ComponentModel.IContainer

    'Hinweis: Die folgende Prozedur ist für den Windows Form-Designer erforderlich.
    'Das Bearbeiten ist mit dem Windows Form-Designer möglich.  
    'Das Bearbeiten mit dem Code-Editor ist nicht möglich.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frm_Connect))
        btnOK = New Button()
        cmbProvider = New ComboBox()
        lblProvider = New Label()
        lblUsername = New Label()
        txtUser = New TextBox()
        lblUserInfo = New Label()
        txtUserInfo = New TextBox()
        bESC = New Button()
        pnlProviderData = New Panel()
        pnlModes = New Panel()
        rdbAgent = New RadioButton()
        rdbChunkExcerpts = New RadioButton()
        lblAGENTexplain = New Label()
        pnlProviderData.SuspendLayout()
        SuspendLayout()
        ' 
        ' btnOK
        ' 
        btnOK.Anchor = AnchorStyles.Bottom
        btnOK.BackColor = SystemColors.GradientInactiveCaption
        btnOK.DialogResult = DialogResult.Cancel
        btnOK.FlatStyle = FlatStyle.Flat
        btnOK.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnOK.ForeColor = SystemColors.WindowText
        btnOK.Location = New Point(312, 655)
        btnOK.Margin = New Padding(4)
        btnOK.Name = "btnOK"
        btnOK.Size = New Size(160, 50)
        btnOK.TabIndex = 5
        btnOK.Text = "btnOK"
        btnOK.TextImageRelation = TextImageRelation.ImageBeforeText
        btnOK.UseVisualStyleBackColor = False
        ' 
        ' cmbProvider
        ' 
        cmbProvider.DropDownStyle = ComboBoxStyle.DropDownList
        cmbProvider.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        cmbProvider.FormattingEnabled = True
        cmbProvider.Location = New Point(30, 196)
        cmbProvider.Margin = New Padding(4)
        cmbProvider.Name = "cmbProvider"
        cmbProvider.Size = New Size(250, 25)
        cmbProvider.TabIndex = 4
        ' 
        ' lblProvider
        ' 
        lblProvider.AutoSize = True
        lblProvider.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblProvider.ForeColor = SystemColors.ActiveCaptionText
        lblProvider.Location = New Point(30, 173)
        lblProvider.Margin = New Padding(4, 0, 4, 0)
        lblProvider.Name = "lblProvider"
        lblProvider.Size = New Size(73, 17)
        lblProvider.TabIndex = 7
        lblProvider.Text = "lblProvider"
        ' 
        ' lblUsername
        ' 
        lblUsername.Anchor = AnchorStyles.Bottom
        lblUsername.AutoSize = True
        lblUsername.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblUsername.ForeColor = SystemColors.ActiveCaptionText
        lblUsername.Location = New Point(13, 501)
        lblUsername.Margin = New Padding(4, 0, 4, 0)
        lblUsername.Name = "lblUsername"
        lblUsername.Size = New Size(83, 17)
        lblUsername.TabIndex = 15
        lblUsername.Text = "lblUsername"
        ' 
        ' txtUser
        ' 
        txtUser.Anchor = AnchorStyles.Bottom
        txtUser.BorderStyle = BorderStyle.None
        txtUser.Location = New Point(15, 524)
        txtUser.Name = "txtUser"
        txtUser.Size = New Size(462, 18)
        txtUser.TabIndex = 14
        ' 
        ' lblUserInfo
        ' 
        lblUserInfo.Anchor = AnchorStyles.Bottom
        lblUserInfo.AutoSize = True
        lblUserInfo.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblUserInfo.ForeColor = SystemColors.ActiveCaptionText
        lblUserInfo.Location = New Point(13, 547)
        lblUserInfo.Margin = New Padding(4, 0, 4, 0)
        lblUserInfo.Name = "lblUserInfo"
        lblUserInfo.Size = New Size(73, 17)
        lblUserInfo.TabIndex = 17
        lblUserInfo.Text = "lblUserInfo"
        ' 
        ' txtUserInfo
        ' 
        txtUserInfo.Anchor = AnchorStyles.Bottom
        txtUserInfo.BorderStyle = BorderStyle.None
        txtUserInfo.Location = New Point(13, 569)
        txtUserInfo.Multiline = True
        txtUserInfo.Name = "txtUserInfo"
        txtUserInfo.Size = New Size(461, 62)
        txtUserInfo.TabIndex = 16
        txtUserInfo.Text = "Give the KI any helpful information about you which may help to understand your dokuments "
        ' 
        ' bESC
        ' 
        bESC.Anchor = AnchorStyles.Bottom
        bESC.BackColor = SystemColors.GradientInactiveCaption
        bESC.DialogResult = DialogResult.Cancel
        bESC.FlatStyle = FlatStyle.Flat
        bESC.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        bESC.ForeColor = SystemColors.WindowText
        bESC.Location = New Point(144, 655)
        bESC.Margin = New Padding(4)
        bESC.Name = "bESC"
        bESC.Size = New Size(160, 50)
        bESC.TabIndex = 18
        bESC.Text = "bEsc"
        bESC.TextImageRelation = TextImageRelation.ImageBeforeText
        bESC.UseVisualStyleBackColor = False
        ' 
        ' pnlProviderData
        ' 
        pnlProviderData.Controls.Add(pnlModes)
        pnlProviderData.Location = New Point(14, 224)
        pnlProviderData.Name = "pnlProviderData"
        pnlProviderData.Size = New Size(460, 195)
        pnlProviderData.TabIndex = 10
        ' 
        ' pnlModes
        ' 
        pnlModes.Location = New Point(233, 287)
        pnlModes.Name = "pnlModes"
        pnlModes.Size = New Size(200, 55)
        pnlModes.TabIndex = 19
        ' 
        ' rdbAgent
        ' 
        rdbAgent.Appearance = Appearance.Button
        rdbAgent.ImageAlign = ContentAlignment.MiddleRight
        rdbAgent.Location = New Point(21, 12)
        rdbAgent.Name = "rdbAgent"
        rdbAgent.Size = New Size(213, 39)
        rdbAgent.TabIndex = 19
        rdbAgent.TabStop = True
        rdbAgent.Text = "Agent mode"
        rdbAgent.TextAlign = ContentAlignment.MiddleCenter
        rdbAgent.UseMnemonic = False
        rdbAgent.UseVisualStyleBackColor = True
        ' 
        ' rdbChunkExcerpts
        ' 
        rdbChunkExcerpts.Appearance = Appearance.Button
        rdbChunkExcerpts.ImageAlign = ContentAlignment.MiddleRight
        rdbChunkExcerpts.Location = New Point(240, 12)
        rdbChunkExcerpts.Name = "rdbChunkExcerpts"
        rdbChunkExcerpts.Size = New Size(224, 39)
        rdbChunkExcerpts.TabIndex = 21
        rdbChunkExcerpts.TabStop = True
        rdbChunkExcerpts.Text = "Guided mode"
        rdbChunkExcerpts.TextAlign = ContentAlignment.MiddleCenter
        rdbChunkExcerpts.UseMnemonic = False
        rdbChunkExcerpts.UseVisualStyleBackColor = True
        ' 
        ' lblAGENTexplain
        ' 
        lblAGENTexplain.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblAGENTexplain.ForeColor = SystemColors.ActiveCaptionText
        lblAGENTexplain.Location = New Point(14, 67)
        lblAGENTexplain.Margin = New Padding(4, 0, 4, 0)
        lblAGENTexplain.Name = "lblAGENTexplain"
        lblAGENTexplain.Size = New Size(446, 88)
        lblAGENTexplain.TabIndex = 22
        lblAGENTexplain.Text = resources.GetString("lblAGENTexplain.Text")
        lblAGENTexplain.TextAlign = ContentAlignment.TopCenter
        ' 
        ' frm_Connect
        ' 
        AutoScaleDimensions = New SizeF(7F, 17F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(486, 720)
        Controls.Add(lblAGENTexplain)
        Controls.Add(rdbChunkExcerpts)
        Controls.Add(rdbAgent)
        Controls.Add(bESC)
        Controls.Add(lblUserInfo)
        Controls.Add(txtUserInfo)
        Controls.Add(lblUsername)
        Controls.Add(txtUser)
        Controls.Add(pnlProviderData)
        Controls.Add(lblProvider)
        Controls.Add(btnOK)
        Controls.Add(cmbProvider)
        FormBorderStyle = FormBorderStyle.FixedDialog
        Name = "frm_Connect"
        Text = "frm_Connect"
        pnlProviderData.ResumeLayout(False)
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend Protected WithEvents btnOK As Button
    Public WithEvents cmbProvider As ComboBox
    Public WithEvents lblProvider As Label
    Public WithEvents lblUsername As Label
    Friend WithEvents TextBox2 As TextBox
    Public WithEvents lblUserInfo As Label
    Friend WithEvents txtUserInfo As TextBox
    Friend Protected WithEvents bESC As Button
    Friend WithEvents pnlProviderData As Panel
    Friend WithEvents txtUser As TextBox
    Friend WithEvents pnlModes As Panel
    Friend WithEvents rdbAgent As RadioButton
    Friend WithEvents rdbChunkExcerpts As RadioButton
    Public WithEvents lblAGENTexplain As Label
End Class
