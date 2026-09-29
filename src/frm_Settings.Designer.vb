<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frm_Settings
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
        lblWorkspace2 = New Label()
        TxtbxWorkspace = New TextBox()
        btnWorkspace = New Button()
        btnINI = New Button()
        btnFolder = New Button()
        btnGuided = New Button()
        btnAgent = New Button()
        lblArchives = New Label()
        lblWorkspace = New Label()
        lblIni = New Label()
        lblArchive2 = New Label()
        btnClose = New Button()
        lblFree = New Label()
        lblBriefing = New Label()
        Label1 = New Label()
        Label2 = New Label()
        numKeepSession = New NumericUpDown()
        chbBigLogs = New CheckBox()
        Label3 = New Label()
        btnReadFirst = New Button()
        btnFinditProfessional = New Button()
        btnGithub = New Button()
        btnModels = New Button()
        Label4 = New Label()
        CType(numKeepSession, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' lblWorkspace2
        ' 
        lblWorkspace2.AutoSize = True
        lblWorkspace2.Font = New Font("Calibri", 12F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        lblWorkspace2.ForeColor = SystemColors.ActiveCaptionText
        lblWorkspace2.Location = New Point(20, 41)
        lblWorkspace2.Margin = New Padding(4, 0, 4, 0)
        lblWorkspace2.Name = "lblWorkspace2"
        lblWorkspace2.Size = New Size(335, 19)
        lblWorkspace2.TabIndex = 7
        lblWorkspace2.Text = "Where all searches, protocols and files are stored."
        ' 
        ' TxtbxWorkspace
        ' 
        TxtbxWorkspace.BorderStyle = BorderStyle.None
        TxtbxWorkspace.Location = New Point(20, 65)
        TxtbxWorkspace.Name = "TxtbxWorkspace"
        TxtbxWorkspace.Size = New Size(641, 18)
        TxtbxWorkspace.TabIndex = 0
        ' 
        ' btnWorkspace
        ' 
        btnWorkspace.BackColor = SystemColors.GradientInactiveCaption
        btnWorkspace.DialogResult = DialogResult.Cancel
        btnWorkspace.FlatStyle = FlatStyle.Flat
        btnWorkspace.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnWorkspace.ForeColor = SystemColors.WindowText
        btnWorkspace.Location = New Point(532, 25)
        btnWorkspace.Margin = New Padding(4)
        btnWorkspace.Name = "btnWorkspace"
        btnWorkspace.Size = New Size(123, 29)
        btnWorkspace.TabIndex = 1
        btnWorkspace.Text = "Change Path"
        btnWorkspace.TextImageRelation = TextImageRelation.ImageBeforeText
        btnWorkspace.UseVisualStyleBackColor = False
        ' 
        ' btnINI
        ' 
        btnINI.BackColor = SystemColors.GradientInactiveCaption
        btnINI.DialogResult = DialogResult.Cancel
        btnINI.FlatStyle = FlatStyle.Flat
        btnINI.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnINI.ForeColor = SystemColors.WindowText
        btnINI.Location = New Point(20, 245)
        btnINI.Margin = New Padding(4)
        btnINI.Name = "btnINI"
        btnINI.Size = New Size(194, 33)
        btnINI.TabIndex = 2
        btnINI.Text = "FinditAgent INI"
        btnINI.TextImageRelation = TextImageRelation.ImageBeforeText
        btnINI.UseVisualStyleBackColor = False
        ' 
        ' btnFolder
        ' 
        btnFolder.BackColor = SystemColors.GradientInactiveCaption
        btnFolder.DialogResult = DialogResult.Cancel
        btnFolder.FlatStyle = FlatStyle.Flat
        btnFolder.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnFolder.ForeColor = SystemColors.WindowText
        btnFolder.Location = New Point(402, 364)
        btnFolder.Margin = New Padding(4)
        btnFolder.Name = "btnFolder"
        btnFolder.Size = New Size(254, 42)
        btnFolder.TabIndex = 6
        btnFolder.Text = "Open Archive Folder"
        btnFolder.UseVisualStyleBackColor = False
        ' 
        ' btnGuided
        ' 
        btnGuided.BackColor = SystemColors.GradientInactiveCaption
        btnGuided.DialogResult = DialogResult.Cancel
        btnGuided.FlatStyle = FlatStyle.Flat
        btnGuided.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnGuided.ForeColor = SystemColors.WindowText
        btnGuided.Location = New Point(20, 197)
        btnGuided.Margin = New Padding(4)
        btnGuided.Name = "btnGuided"
        btnGuided.Size = New Size(194, 34)
        btnGuided.TabIndex = 3
        btnGuided.Text = "About Guided Mode"
        btnGuided.TextImageRelation = TextImageRelation.ImageBeforeText
        btnGuided.UseVisualStyleBackColor = False
        ' 
        ' btnAgent
        ' 
        btnAgent.BackColor = SystemColors.GradientInactiveCaption
        btnAgent.DialogResult = DialogResult.Cancel
        btnAgent.FlatStyle = FlatStyle.Flat
        btnAgent.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnAgent.ForeColor = SystemColors.WindowText
        btnAgent.Location = New Point(20, 152)
        btnAgent.Margin = New Padding(4)
        btnAgent.Name = "btnAgent"
        btnAgent.Size = New Size(193, 33)
        btnAgent.TabIndex = 4
        btnAgent.Text = "About Agent Mode"
        btnAgent.TextImageRelation = TextImageRelation.ImageBeforeText
        btnAgent.UseVisualStyleBackColor = False
        ' 
        ' lblArchives
        ' 
        lblArchives.AutoSize = True
        lblArchives.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblArchives.ForeColor = SystemColors.ActiveCaptionText
        lblArchives.Location = New Point(20, 347)
        lblArchives.Margin = New Padding(4, 0, 4, 0)
        lblArchives.Name = "lblArchives"
        lblArchives.Size = New Size(121, 19)
        lblArchives.TabIndex = 20
        lblArchives.Text = "Session Archives"
        ' 
        ' lblWorkspace
        ' 
        lblWorkspace.AutoSize = True
        lblWorkspace.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblWorkspace.ForeColor = SystemColors.ActiveCaptionText
        lblWorkspace.Location = New Point(20, 20)
        lblWorkspace.Margin = New Padding(4, 0, 4, 0)
        lblWorkspace.Name = "lblWorkspace"
        lblWorkspace.Size = New Size(133, 19)
        lblWorkspace.TabIndex = 21
        lblWorkspace.Text = "AgentWorkspace: "
        ' 
        ' lblIni
        ' 
        lblIni.AutoSize = True
        lblIni.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblIni.ForeColor = SystemColors.ActiveCaptionText
        lblIni.Location = New Point(226, 203)
        lblIni.Margin = New Padding(4, 0, 4, 0)
        lblIni.Name = "lblIni"
        lblIni.Size = New Size(319, 19)
        lblIni.TabIndex = 22
        lblIni.Text = "Details and links to Guided mode markdowns"
        ' 
        ' lblArchive2
        ' 
        lblArchive2.Font = New Font("Calibri", 12F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        lblArchive2.ForeColor = SystemColors.ActiveCaptionText
        lblArchive2.Location = New Point(20, 368)
        lblArchive2.Margin = New Padding(4, 0, 4, 0)
        lblArchive2.Name = "lblArchive2"
        lblArchive2.Size = New Size(374, 42)
        lblArchive2.TabIndex = 23
        lblArchive2.Text = "Each session is archived in its own directory, including" & vbLf & "the protocol, log file and all Findit search files."
        ' 
        ' btnClose
        ' 
        btnClose.BackColor = SystemColors.GradientInactiveCaption
        btnClose.DialogResult = DialogResult.Cancel
        btnClose.FlatStyle = FlatStyle.Flat
        btnClose.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnClose.ForeColor = SystemColors.WindowText
        btnClose.Location = New Point(402, 528)
        btnClose.Margin = New Padding(4)
        btnClose.Name = "btnClose"
        btnClose.Size = New Size(254, 78)
        btnClose.TabIndex = 7
        btnClose.Text = "Close"
        btnClose.UseVisualStyleBackColor = False
        ' 
        ' lblFree
        ' 
        lblFree.AutoSize = True
        lblFree.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblFree.ForeColor = SystemColors.ActiveCaptionText
        lblFree.Location = New Point(226, 251)
        lblFree.Margin = New Padding(4, 0, 4, 0)
        lblFree.Name = "lblFree"
        lblFree.Size = New Size(409, 19)
        lblFree.TabIndex = 26
        lblFree.Text = "Findit6Agent INI:  Edit providers, models, moods and more"
        ' 
        ' lblBriefing
        ' 
        lblBriefing.AutoSize = True
        lblBriefing.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblBriefing.ForeColor = SystemColors.ActiveCaptionText
        lblBriefing.Location = New Point(226, 158)
        lblBriefing.Margin = New Padding(4, 0, 4, 0)
        lblBriefing.Name = "lblBriefing"
        lblBriefing.Size = New Size(311, 19)
        lblBriefing.TabIndex = 27
        lblBriefing.Text = "Details and links to Agent mode markdowns"
        ' 
        ' Label1
        ' 
        Label1.Font = New Font("Calibri", 12F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        Label1.ForeColor = SystemColors.ActiveCaptionText
        Label1.Location = New Point(211, 430)
        Label1.Margin = New Padding(4, 0, 4, 0)
        Label1.Name = "Label1"
        Label1.Size = New Size(387, 31)
        Label1.TabIndex = 29
        Label1.Text = "Each of them will be deleted  after this number of weeks"
        Label1.TextAlign = ContentAlignment.MiddleRight
        ' 
        ' Label2
        ' 
        Label2.Font = New Font("Calibri", 12F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        Label2.ForeColor = SystemColors.ActiveCaptionText
        Label2.Location = New Point(211, 410)
        Label2.Margin = New Padding(4, 0, 4, 0)
        Label2.Name = "Label2"
        Label2.Size = New Size(387, 31)
        Label2.TabIndex = 30
        Label2.Text = "To avoid endless numbers of  sessionArchives: "
        Label2.TextAlign = ContentAlignment.MiddleRight
        ' 
        ' numKeepSession
        ' 
        numKeepSession.Font = New Font("Segoe UI", 14.25F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        numKeepSession.Location = New Point(605, 419)
        numKeepSession.Name = "numKeepSession"
        numKeepSession.Size = New Size(50, 33)
        numKeepSession.TabIndex = 31
        numKeepSession.TextAlign = HorizontalAlignment.Center
        numKeepSession.Value = New Decimal(New Integer() {2, 0, 0, 0})
        ' 
        ' chbBigLogs
        ' 
        chbBigLogs.AutoSize = True
        chbBigLogs.CheckAlign = ContentAlignment.MiddleRight
        chbBigLogs.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        chbBigLogs.Location = New Point(261, 478)
        chbBigLogs.Name = "chbBigLogs"
        chbBigLogs.Size = New Size(394, 21)
        chbBigLogs.TabIndex = 32
        chbBigLogs.Text = "Logfiles includig prompts. quite big but sometimes helpful"
        chbBigLogs.UseVisualStyleBackColor = True
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label3.ForeColor = SystemColors.ActiveCaptionText
        Label3.Location = New Point(226, 115)
        Label3.Margin = New Padding(4, 0, 4, 0)
        Label3.Name = "Label3"
        Label3.Size = New Size(154, 19)
        Label3.TabIndex = 33
        Label3.Text = "First start - Read me: "
        ' 
        ' btnReadFirst
        ' 
        btnReadFirst.BackColor = SystemColors.GradientInactiveCaption
        btnReadFirst.DialogResult = DialogResult.Cancel
        btnReadFirst.FlatStyle = FlatStyle.Flat
        btnReadFirst.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnReadFirst.ForeColor = SystemColors.WindowText
        btnReadFirst.Location = New Point(20, 109)
        btnReadFirst.Margin = New Padding(4)
        btnReadFirst.Name = "btnReadFirst"
        btnReadFirst.Size = New Size(194, 31)
        btnReadFirst.TabIndex = 34
        btnReadFirst.Text = "Read me first"
        btnReadFirst.TextImageRelation = TextImageRelation.ImageBeforeText
        btnReadFirst.UseVisualStyleBackColor = False
        ' 
        ' btnFinditProfessional
        ' 
        btnFinditProfessional.BackColor = SystemColors.GradientInactiveCaption
        btnFinditProfessional.DialogResult = DialogResult.Cancel
        btnFinditProfessional.FlatStyle = FlatStyle.Flat
        btnFinditProfessional.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnFinditProfessional.ForeColor = SystemColors.WindowText
        btnFinditProfessional.Location = New Point(20, 575)
        btnFinditProfessional.Margin = New Padding(4)
        btnFinditProfessional.Name = "btnFinditProfessional"
        btnFinditProfessional.Size = New Size(193, 36)
        btnFinditProfessional.TabIndex = 35
        btnFinditProfessional.Text = "Findit for professional use"
        btnFinditProfessional.TextImageRelation = TextImageRelation.ImageBeforeText
        btnFinditProfessional.UseVisualStyleBackColor = False
        ' 
        ' btnGithub
        ' 
        btnGithub.BackColor = SystemColors.GradientInactiveCaption
        btnGithub.DialogResult = DialogResult.Cancel
        btnGithub.FlatStyle = FlatStyle.Flat
        btnGithub.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnGithub.ForeColor = SystemColors.WindowText
        btnGithub.Location = New Point(20, 528)
        btnGithub.Margin = New Padding(4)
        btnGithub.Name = "btnGithub"
        btnGithub.Size = New Size(193, 36)
        btnGithub.TabIndex = 36
        btnGithub.Text = "Github source reposity"
        btnGithub.TextImageRelation = TextImageRelation.ImageBeforeText
        btnGithub.UseVisualStyleBackColor = False
        ' 
        ' btnModels
        ' 
        btnModels.BackColor = SystemColors.GradientInactiveCaption
        btnModels.DialogResult = DialogResult.Cancel
        btnModels.FlatStyle = FlatStyle.Flat
        btnModels.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnModels.ForeColor = SystemColors.WindowText
        btnModels.Location = New Point(20, 290)
        btnModels.Margin = New Padding(4)
        btnModels.Name = "btnModels"
        btnModels.Size = New Size(194, 33)
        btnModels.TabIndex = 37
        btnModels.Text = "Providers and Models"
        btnModels.TextImageRelation = TextImageRelation.ImageBeforeText
        btnModels.UseVisualStyleBackColor = False
        ' 
        ' Label4
        ' 
        Label4.AutoSize = True
        Label4.Font = New Font("Calibri", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        Label4.ForeColor = SystemColors.ActiveCaptionText
        Label4.Location = New Point(226, 297)
        Label4.Margin = New Padding(4, 0, 4, 0)
        Label4.Name = "Label4"
        Label4.Size = New Size(270, 19)
        Label4.TabIndex = 38
        Label4.Text = "Different providers,models, behaviour"
        ' 
        ' frm_Settings
        ' 
        AutoScaleDimensions = New SizeF(7F, 17F)
        AutoScaleMode = AutoScaleMode.Font
        CancelButton = btnClose
        ClientSize = New Size(688, 633)
        Controls.Add(Label4)
        Controls.Add(btnModels)
        Controls.Add(btnGithub)
        Controls.Add(btnFinditProfessional)
        Controls.Add(btnReadFirst)
        Controls.Add(Label3)
        Controls.Add(chbBigLogs)
        Controls.Add(numKeepSession)
        Controls.Add(Label2)
        Controls.Add(Label1)
        Controls.Add(lblBriefing)
        Controls.Add(lblFree)
        Controls.Add(btnClose)
        Controls.Add(lblArchive2)
        Controls.Add(lblIni)
        Controls.Add(lblWorkspace)
        Controls.Add(lblArchives)
        Controls.Add(btnAgent)
        Controls.Add(btnGuided)
        Controls.Add(btnFolder)
        Controls.Add(btnINI)
        Controls.Add(btnWorkspace)
        Controls.Add(TxtbxWorkspace)
        Controls.Add(lblWorkspace2)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "frm_Settings"
        Text = " Findit6Agent basic settings"
        CType(numKeepSession, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Public WithEvents lblWorkspace2 As Label
    Friend WithEvents TxtbxWorkspace As TextBox
    Friend Protected WithEvents btnWorkspace As Button
    Friend Protected WithEvents btnINI As Button
    Friend Protected WithEvents btnFolder As Button
    Friend Protected WithEvents btnGuided As Button
    Friend Protected WithEvents btnAgent As Button
    Public WithEvents lblArchives As Label
    Public WithEvents lblWorkspace As Label
    Public WithEvents lblIni As Label
    Public WithEvents lblArchive2 As Label
    Friend Protected WithEvents btnClose As Button
    Public WithEvents lblFree As Label
    Public WithEvents lblBriefing As Label
    Public WithEvents Label1 As Label
    Public WithEvents Label2 As Label
    Friend WithEvents numKeepSession As NumericUpDown
    Private WithEvents chbBigLogs As CheckBox
    Public WithEvents Label3 As Label
    Friend Protected WithEvents btnReadFirst As Button
    Friend Protected WithEvents btnFinditProfessional As Button
    Friend Protected WithEvents btnGithub As Button
    Friend Protected WithEvents btnModels As Button
    Public WithEvents Label4 As Label
End Class
