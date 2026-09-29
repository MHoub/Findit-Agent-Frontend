<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmMain
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmMain))
        lblRTFbox = New Label()
        lblPrompt = New Label()
        txtPrompt = New TextBox()
        btnSend = New Button()
        btnConnect = New Button()
        ImageList1 = New ImageList(components)
        lblTokensUsed = New Label()
        lblWhere = New Label()
        btnWhere = New Button()
        lblCurrentState = New Label()
        rtfbProtokoll = New RichTextBox()
        btnSettings = New Button()
        chkbOCR = New CheckBox()
        pnlWhere = New Panel()
        txtWhere = New TextBox()
        btnCurDir = New Button()
        Spinner = New PictureBox()
        rtfKopie = New RichTextBox()
        CType(Spinner, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' lblRTFbox
        ' 
        lblRTFbox.AutoSize = True
        lblRTFbox.Font = New Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        lblRTFbox.ForeColor = Color.White
        lblRTFbox.Location = New Point(15, 75)
        lblRTFbox.Name = "lblRTFbox"
        lblRTFbox.Size = New Size(342, 20)
        lblRTFbox.TabIndex = 2
        lblRTFbox.Text = "Complete communication in chronological order"
        ' 
        ' lblPrompt
        ' 
        lblPrompt.AutoSize = True
        lblPrompt.Font = New Font("Microsoft Sans Serif", 12F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        lblPrompt.ForeColor = Color.White
        lblPrompt.Location = New Point(14, 461)
        lblPrompt.Name = "lblPrompt"
        lblPrompt.Size = New Size(106, 20)
        lblPrompt.TabIndex = 3
        lblPrompt.Text = "User Prompts"
        ' 
        ' txtPrompt
        ' 
        txtPrompt.BackColor = Color.LightGray
        txtPrompt.BorderStyle = BorderStyle.None
        txtPrompt.Font = New Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        txtPrompt.ForeColor = Color.Black
        txtPrompt.Location = New Point(17, 483)
        txtPrompt.Multiline = True
        txtPrompt.Name = "txtPrompt"
        txtPrompt.Size = New Size(698, 95)
        txtPrompt.TabIndex = 6
        ' 
        ' btnSend
        ' 
        btnSend.BackColor = Color.DimGray
        btnSend.Enabled = False
        btnSend.FlatAppearance.BorderColor = Color.Silver
        btnSend.FlatStyle = FlatStyle.Flat
        btnSend.Font = New Font("Microsoft Sans Serif", 9.75F)
        btnSend.ForeColor = SystemColors.ControlLightLight
        btnSend.Location = New Point(517, 601)
        btnSend.Name = "btnSend"
        btnSend.Size = New Size(194, 35)
        btnSend.TabIndex = 7
        btnSend.Text = "awaiting connection"
        btnSend.UseVisualStyleBackColor = False
        ' 
        ' btnConnect
        ' 
        btnConnect.BackColor = Color.DimGray
        btnConnect.FlatAppearance.BorderColor = Color.Silver
        btnConnect.FlatStyle = FlatStyle.Flat
        btnConnect.Font = New Font("Microsoft Sans Serif", 9.75F)
        btnConnect.ForeColor = SystemColors.ControlLightLight
        btnConnect.ImageAlign = ContentAlignment.MiddleLeft
        btnConnect.ImageKey = "OFF"
        btnConnect.ImageList = ImageList1
        btnConnect.Location = New Point(402, 17)
        btnConnect.Name = "btnConnect"
        btnConnect.Padding = New Padding(46, 0, 0, 0)
        btnConnect.Size = New Size(251, 48)
        btnConnect.TabIndex = 3
        btnConnect.Text = "Connect LLM"
        btnConnect.UseVisualStyleBackColor = False
        ' 
        ' ImageList1
        ' 
        ImageList1.ColorDepth = ColorDepth.Depth32Bit
        ImageList1.ImageStream = CType(resources.GetObject("ImageList1.ImageStream"), ImageListStreamer)
        ImageList1.TransparentColor = Color.Transparent
        ImageList1.Images.SetKeyName(0, "OFF")
        ImageList1.Images.SetKeyName(1, "Connecting")
        ImageList1.Images.SetKeyName(2, "Local")
        ImageList1.Images.SetKeyName(3, "Extern")
        ImageList1.Images.SetKeyName(4, "Extern.jpg")
        ' 
        ' lblTokensUsed
        ' 
        lblTokensUsed.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        lblTokensUsed.AutoSize = True
        lblTokensUsed.Font = New Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblTokensUsed.ForeColor = Color.White
        lblTokensUsed.Location = New Point(540, 443)
        lblTokensUsed.Name = "lblTokensUsed"
        lblTokensUsed.Size = New Size(113, 16)
        lblTokensUsed.TabIndex = 12
        lblTokensUsed.Text = "lblTokensUsed"
        ' 
        ' lblWhere
        ' 
        lblWhere.AutoSize = True
        lblWhere.Font = New Font("Microsoft Sans Serif", 11.25F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        lblWhere.ForeColor = Color.White
        lblWhere.Location = New Point(14, 13)
        lblWhere.Name = "lblWhere"
        lblWhere.Size = New Size(133, 18)
        lblWhere.TabIndex = 15
        lblWhere.Text = "WHERE to Search"
        ' 
        ' btnWhere
        ' 
        btnWhere.BackColor = Color.DimGray
        btnWhere.FlatAppearance.BorderColor = Color.Silver
        btnWhere.FlatStyle = FlatStyle.Flat
        btnWhere.Font = New Font("Microsoft Sans Serif", 8.25F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        btnWhere.ForeColor = SystemColors.ControlLightLight
        btnWhere.Location = New Point(303, 36)
        btnWhere.Name = "btnWhere"
        btnWhere.Size = New Size(79, 29)
        btnWhere.TabIndex = 2
        btnWhere.Text = "ooo"
        btnWhere.UseVisualStyleBackColor = False
        ' 
        ' lblCurrentState
        ' 
        lblCurrentState.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        lblCurrentState.AutoSize = True
        lblCurrentState.Font = New Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblCurrentState.ForeColor = Color.MistyRose
        lblCurrentState.Location = New Point(14, 606)
        lblCurrentState.Name = "lblCurrentState"
        lblCurrentState.Size = New Size(217, 16)
        lblCurrentState.TabIndex = 18
        lblCurrentState.Text = "RateLimit Warning, waiting 10s"
        ' 
        ' rtfbProtokoll
        ' 
        rtfbProtokoll.BackColor = Color.DimGray
        rtfbProtokoll.BorderStyle = BorderStyle.None
        rtfbProtokoll.Font = New Font("Microsoft Sans Serif", 11.25F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        rtfbProtokoll.Location = New Point(17, 101)
        rtfbProtokoll.Name = "rtfbProtokoll"
        rtfbProtokoll.ReadOnly = True
        rtfbProtokoll.Size = New Size(701, 332)
        rtfbProtokoll.TabIndex = 5
        rtfbProtokoll.TabStop = False
        rtfbProtokoll.Text = ""
        ' 
        ' btnSettings
        ' 
        btnSettings.BackColor = Color.DimGray
        btnSettings.FlatAppearance.BorderColor = Color.Silver
        btnSettings.FlatStyle = FlatStyle.Flat
        btnSettings.Font = New Font("Microsoft Sans Serif", 9.75F)
        btnSettings.ForeColor = SystemColors.ControlLightLight
        btnSettings.Image = CType(resources.GetObject("btnSettings.Image"), Image)
        btnSettings.ImageAlign = ContentAlignment.MiddleLeft
        btnSettings.Location = New Point(659, 17)
        btnSettings.Name = "btnSettings"
        btnSettings.Padding = New Padding(5, 0, 0, 0)
        btnSettings.Size = New Size(56, 48)
        btnSettings.TabIndex = 4
        btnSettings.UseVisualStyleBackColor = False
        ' 
        ' chkbOCR
        ' 
        chkbOCR.AutoSize = True
        chkbOCR.CheckAlign = ContentAlignment.MiddleRight
        chkbOCR.ForeColor = Color.White
        chkbOCR.Location = New Point(303, 14)
        chkbOCR.Name = "chkbOCR"
        chkbOCR.Size = New Size(83, 20)
        chkbOCR.TabIndex = 19
        chkbOCR.Text = "Use OCR"
        chkbOCR.UseVisualStyleBackColor = True
        ' 
        ' pnlWhere
        ' 
        pnlWhere.AutoSizeMode = AutoSizeMode.GrowAndShrink
        pnlWhere.BackColor = Color.LightGray
        pnlWhere.Location = New Point(12, 34)
        pnlWhere.Name = "pnlWhere"
        pnlWhere.Size = New Size(285, 31)
        pnlWhere.TabIndex = 20
        ' 
        ' txtWhere
        ' 
        txtWhere.BackColor = Color.LightGray
        txtWhere.BorderStyle = BorderStyle.None
        txtWhere.Font = New Font("Microsoft Sans Serif", 11.25F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        txtWhere.ForeColor = Color.Black
        txtWhere.Location = New Point(17, 40)
        txtWhere.Margin = New Padding(0)
        txtWhere.Name = "txtWhere"
        txtWhere.Size = New Size(280, 17)
        txtWhere.TabIndex = 2
        txtWhere.Text = "  d:\Archiv & Subs"
        ' 
        ' btnCurDir
        ' 
        btnCurDir.BackColor = Color.DimGray
        btnCurDir.FlatAppearance.BorderColor = Color.Silver
        btnCurDir.FlatStyle = FlatStyle.Flat
        btnCurDir.Font = New Font("Microsoft Sans Serif", 9.75F)
        btnCurDir.ForeColor = SystemColors.ControlLightLight
        btnCurDir.Image = CType(resources.GetObject("btnCurDir.Image"), Image)
        btnCurDir.ImageAlign = ContentAlignment.MiddleLeft
        btnCurDir.Location = New Point(659, 443)
        btnCurDir.Name = "btnCurDir"
        btnCurDir.Padding = New Padding(10, 0, 0, 0)
        btnCurDir.Size = New Size(56, 29)
        btnCurDir.TabIndex = 21
        btnCurDir.UseVisualStyleBackColor = False
        btnCurDir.Visible = False
        ' 
        ' Spinner
        ' 
        Spinner.Image = CType(resources.GetObject("Spinner.Image"), Image)
        Spinner.Location = New Point(17, 637)
        Spinner.Name = "Spinner"
        Spinner.Size = New Size(48, 42)
        Spinner.SizeMode = PictureBoxSizeMode.StretchImage
        Spinner.TabIndex = 22
        Spinner.TabStop = False
        Spinner.Visible = False
        ' 
        ' rtfKopie
        ' 
        rtfKopie.BackColor = Color.DimGray
        rtfKopie.BorderStyle = BorderStyle.None
        rtfKopie.Font = New Font("Microsoft Sans Serif", 11.25F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        rtfKopie.Location = New Point(340, 601)
        rtfKopie.Name = "rtfKopie"
        rtfKopie.ReadOnly = True
        rtfKopie.Size = New Size(110, 39)
        rtfKopie.TabIndex = 23
        rtfKopie.TabStop = False
        rtfKopie.Text = ""
        rtfKopie.Visible = False
        ' 
        ' frmMain
        ' 
        AutoScaleMode = AutoScaleMode.None
        BackColor = Color.Gray
        ClientSize = New Size(740, 734)
        Controls.Add(rtfKopie)
        Controls.Add(Spinner)
        Controls.Add(btnCurDir)
        Controls.Add(txtWhere)
        Controls.Add(pnlWhere)
        Controls.Add(chkbOCR)
        Controls.Add(btnSettings)
        Controls.Add(rtfbProtokoll)
        Controls.Add(lblCurrentState)
        Controls.Add(btnWhere)
        Controls.Add(lblWhere)
        Controls.Add(lblTokensUsed)
        Controls.Add(btnConnect)
        Controls.Add(btnSend)
        Controls.Add(txtPrompt)
        Controls.Add(lblPrompt)
        Controls.Add(lblRTFbox)
        Font = New Font("Microsoft Sans Serif", 9.75F)
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        MinimumSize = New Size(713, 730)
        Name = "frmMain"
        Text = "Findit 6 for Agents"
        TransparencyKey = Color.Brown
        CType(Spinner, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
        PerformLayout()
    End Sub
    Friend WithEvents lblRTFbox As Label
    Friend WithEvents lblPrompt As Label
    Friend WithEvents txtPrompt As TextBox
    Friend WithEvents btnSend As Button
    Friend WithEvents btnConnect As Button
    Friend WithEvents lblTokensUsed As Label
    Friend WithEvents lblWhere As Label
    Friend WithEvents btnWhere As Button
    Friend WithEvents lblCurrentState As Label
    Friend WithEvents ImageList1 As ImageList
    Friend WithEvents btnStoreprotokoll As Button
    Friend WithEvents rtfbProtokoll As RichTextBox
    Friend WithEvents btnSettings As Button
    Friend WithEvents chkbOCR As CheckBox
    Friend WithEvents pnlWhere As Panel
    Friend WithEvents txtWhere As TextBox
    Friend WithEvents btnCurDir As Button
    Friend WithEvents Spinner As PictureBox
    Friend WithEvents rtfKopie As RichTextBox

End Class
