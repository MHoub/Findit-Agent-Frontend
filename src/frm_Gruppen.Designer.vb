<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frm_Gruppen
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frm_Gruppen))
        TreeView1 = New TreeView()
        ImageList1 = New ImageList(components)
        ContextMenuStrip1 = New ContextMenuStrip(components)
        ToolStripMenuItem1 = New ToolStripMenuItem()
        bCancel = New Button()
        bOK = New Button()
        cmbGruppen = New ComboBox()
        bNeu = New Button()
        lblGruppe = New Label()
        lblPfade = New Label()
        tTip = New ToolTip(components)
        bDel = New Button()
        bRename = New Button()
        TxtbxBeschreibung = New TextBox()
        lblBeschreibung = New Label()
        ContextMenuStrip1.SuspendLayout()
        SuspendLayout()
        ' 
        ' TreeView1
        ' 
        TreeView1.BorderStyle = BorderStyle.None
        TreeView1.Font = New Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        TreeView1.HideSelection = False
        TreeView1.ImageIndex = 0
        TreeView1.ImageList = ImageList1
        TreeView1.ImeMode = ImeMode.NoControl
        TreeView1.LineColor = Color.White
        TreeView1.Location = New Point(25, 187)
        TreeView1.Margin = New Padding(13, 12, 13, 12)
        TreeView1.Name = "TreeView1"
        TreeView1.SelectedImageIndex = 0
        TreeView1.ShowLines = False
        TreeView1.Size = New Size(395, 491)
        TreeView1.TabIndex = 6
        ' 
        ' ImageList1
        ' 
        ImageList1.ColorDepth = ColorDepth.Depth8Bit
        ImageList1.ImageStream = CType(resources.GetObject("ImageList1.ImageStream"), ImageListStreamer)
        ImageList1.TransparentColor = Color.Transparent
        ImageList1.Images.SetKeyName(0, "hd.gif")
        ImageList1.Images.SetKeyName(1, "folder")
        ImageList1.Images.SetKeyName(2, "usb.gif")
        ImageList1.Images.SetKeyName(3, "hd.gif")
        ImageList1.Images.SetKeyName(4, "Netz.gif")
        ImageList1.Images.SetKeyName(5, "cd.gif")
        ImageList1.Images.SetKeyName(6, "hd.gif")
        ImageList1.Images.SetKeyName(7, "system.gif")
        ImageList1.Images.SetKeyName(8, "i_fav.jpg")
        ImageList1.Images.SetKeyName(9, "desktop.gif")
        ImageList1.Images.SetKeyName(10, "Dokumente.gif")
        ImageList1.Images.SetKeyName(11, "pics.gif")
        ImageList1.Images.SetKeyName(12, "Musik.gif")
        ImageList1.Images.SetKeyName(13, "i_comp.jpg")
        ImageList1.Images.SetKeyName(14, "folderNotk.gif")
        ImageList1.Images.SetKeyName(15, "OneDrive.gif")
        ' 
        ' ContextMenuStrip1
        ' 
        ContextMenuStrip1.Items.AddRange(New ToolStripItem() {ToolStripMenuItem1})
        ContextMenuStrip1.Name = "ContextMenuStrip1"
        ContextMenuStrip1.Size = New Size(214, 26)
        ' 
        ' ToolStripMenuItem1
        ' 
        ToolStripMenuItem1.Name = "ToolStripMenuItem1"
        ToolStripMenuItem1.Size = New Size(213, 22)
        ToolStripMenuItem1.Text = "Neuen Ordner erstellen"
        ' 
        ' bCancel
        ' 
        bCancel.BackColor = SystemColors.ButtonFace
        bCancel.DialogResult = DialogResult.Cancel
        bCancel.FlatStyle = FlatStyle.Flat
        bCancel.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        bCancel.Location = New Point(106, 694)
        bCancel.Margin = New Padding(4)
        bCancel.Name = "bCancel"
        bCancel.Size = New Size(155, 42)
        bCancel.TabIndex = 1
        bCancel.TabStop = False
        bCancel.Text = "bEsc"
        bCancel.UseVisualStyleBackColor = False
        ' 
        ' bOK
        ' 
        bOK.BackColor = SystemColors.ButtonFace
        bOK.FlatStyle = FlatStyle.Flat
        bOK.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        bOK.Location = New Point(269, 694)
        bOK.Margin = New Padding(4)
        bOK.Name = "bOK"
        bOK.Size = New Size(160, 42)
        bOK.TabIndex = 7
        bOK.Text = "bOK"
        bOK.UseVisualStyleBackColor = False
        ' 
        ' cmbGruppen
        ' 
        cmbGruppen.DropDownStyle = ComboBoxStyle.DropDownList
        cmbGruppen.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        cmbGruppen.FormattingEnabled = True
        cmbGruppen.Location = New Point(23, 37)
        cmbGruppen.Margin = New Padding(4)
        cmbGruppen.Name = "cmbGruppen"
        cmbGruppen.Size = New Size(279, 25)
        cmbGruppen.TabIndex = 1
        cmbGruppen.Visible = False
        ' 
        ' bNeu
        ' 
        bNeu.BackColor = SystemColors.GradientInactiveCaption
        bNeu.DialogResult = DialogResult.Cancel
        bNeu.FlatStyle = FlatStyle.Flat
        bNeu.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        bNeu.ForeColor = SystemColors.WindowText
        bNeu.Location = New Point(311, 108)
        bNeu.Margin = New Padding(4)
        bNeu.Name = "bNeu"
        bNeu.Size = New Size(109, 28)
        bNeu.TabIndex = 4
        bNeu.Text = "bNeu"
        bNeu.TextAlign = ContentAlignment.BottomCenter
        bNeu.TextImageRelation = TextImageRelation.ImageBeforeText
        tTip.SetToolTip(bNeu, "Neue Pfadgruppe erzeugen")
        bNeu.UseVisualStyleBackColor = False
        bNeu.Visible = False
        ' 
        ' lblGruppe
        ' 
        lblGruppe.AutoSize = True
        lblGruppe.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblGruppe.ForeColor = SystemColors.ActiveCaptionText
        lblGruppe.Location = New Point(21, 16)
        lblGruppe.Margin = New Padding(4, 0, 4, 0)
        lblGruppe.Name = "lblGruppe"
        lblGruppe.Size = New Size(67, 17)
        lblGruppe.TabIndex = 6
        lblGruppe.Text = "lblGruppe"
        lblGruppe.Visible = False
        ' 
        ' lblPfade
        ' 
        lblPfade.AutoSize = True
        lblPfade.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblPfade.ForeColor = SystemColors.ActiveCaptionText
        lblPfade.Location = New Point(21, 158)
        lblPfade.Margin = New Padding(4, 0, 4, 0)
        lblPfade.Name = "lblPfade"
        lblPfade.Size = New Size(56, 17)
        lblPfade.TabIndex = 7
        lblPfade.Text = "lblPfade"
        lblPfade.Visible = False
        ' 
        ' tTip
        ' 
        tTip.AutomaticDelay = 1000
        ' 
        ' bDel
        ' 
        bDel.BackColor = SystemColors.GradientInactiveCaption
        bDel.DialogResult = DialogResult.Cancel
        bDel.FlatStyle = FlatStyle.Flat
        bDel.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        bDel.ForeColor = SystemColors.WindowText
        bDel.Location = New Point(309, 72)
        bDel.Margin = New Padding(4)
        bDel.Name = "bDel"
        bDel.Size = New Size(110, 28)
        bDel.TabIndex = 5
        bDel.Text = "bDel"
        bDel.TextAlign = ContentAlignment.BottomCenter
        bDel.TextImageRelation = TextImageRelation.ImageBeforeText
        tTip.SetToolTip(bDel, "Gewählte Pfadgruppe löschen")
        bDel.UseVisualStyleBackColor = False
        bDel.Visible = False
        ' 
        ' bRename
        ' 
        bRename.BackColor = SystemColors.GradientInactiveCaption
        bRename.DialogResult = DialogResult.Cancel
        bRename.FlatStyle = FlatStyle.Flat
        bRename.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        bRename.ForeColor = SystemColors.WindowText
        bRename.Location = New Point(310, 37)
        bRename.Margin = New Padding(4)
        bRename.Name = "bRename"
        bRename.Size = New Size(109, 25)
        bRename.TabIndex = 3
        bRename.Text = "bRename"
        bRename.TextAlign = ContentAlignment.BottomCenter
        bRename.TextImageRelation = TextImageRelation.ImageBeforeText
        tTip.SetToolTip(bRename, "Gewählte Pfadgruppe löschen")
        bRename.UseVisualStyleBackColor = False
        ' 
        ' TxtbxBeschreibung
        ' 
        TxtbxBeschreibung.BorderStyle = BorderStyle.None
        TxtbxBeschreibung.Location = New Point(24, 72)
        TxtbxBeschreibung.Multiline = True
        TxtbxBeschreibung.Name = "TxtbxBeschreibung"
        TxtbxBeschreibung.Size = New Size(278, 62)
        TxtbxBeschreibung.TabIndex = 2
        ' 
        ' lblBeschreibung
        ' 
        lblBeschreibung.AutoSize = True
        lblBeschreibung.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblBeschreibung.ForeColor = SystemColors.ActiveCaptionText
        lblBeschreibung.Location = New Point(27, 75)
        lblBeschreibung.Margin = New Padding(4, 0, 4, 0)
        lblBeschreibung.Name = "lblBeschreibung"
        lblBeschreibung.Size = New Size(104, 17)
        lblBeschreibung.TabIndex = 10
        lblBeschreibung.Text = "lblBeschreibung"
        lblBeschreibung.Visible = False
        ' 
        ' frm_Gruppen
        ' 
        AcceptButton = bOK
        AutoScaleMode = AutoScaleMode.None
        BackColor = SystemColors.ActiveCaption
        CancelButton = bCancel
        ClientSize = New Size(447, 749)
        Controls.Add(bRename)
        Controls.Add(lblBeschreibung)
        Controls.Add(TxtbxBeschreibung)
        Controls.Add(bDel)
        Controls.Add(TreeView1)
        Controls.Add(lblPfade)
        Controls.Add(lblGruppe)
        Controls.Add(bNeu)
        Controls.Add(cmbGruppen)
        Controls.Add(bOK)
        Controls.Add(bCancel)
        Font = New Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        ForeColor = SystemColors.ActiveCaptionText
        FormBorderStyle = FormBorderStyle.FixedDialog
        Margin = New Padding(4)
        MaximizeBox = False
        MinimizeBox = False
        Name = "frm_Gruppen"
        ShowIcon = False
        ShowInTaskbar = False
        SizeGripStyle = SizeGripStyle.Hide
        StartPosition = FormStartPosition.CenterParent
        Text = "frm_TreePfad"
        ContextMenuStrip1.ResumeLayout(False)
        ResumeLayout(False)
        PerformLayout()

    End Sub
    Public WithEvents TreeView1 As System.Windows.Forms.TreeView
    Public WithEvents ImageList1 As System.Windows.Forms.ImageList
    Public WithEvents bCancel As System.Windows.Forms.Button
    Public WithEvents bOK As System.Windows.Forms.Button
    Public WithEvents cmbGruppen As System.Windows.Forms.ComboBox
    Public WithEvents bNeu As System.Windows.Forms.Button
    Public WithEvents lblGruppe As System.Windows.Forms.Label
    Public WithEvents lblPfade As System.Windows.Forms.Label
    Public WithEvents tTip As System.Windows.Forms.ToolTip
    Public WithEvents bDel As System.Windows.Forms.Button
    Friend WithEvents ContextMenuStrip1 As ContextMenuStrip
    Friend WithEvents ToolStripMenuItem1 As ToolStripMenuItem
    Friend WithEvents TxtbxBeschreibung As TextBox
    Public WithEvents lblBeschreibung As Label
    Protected Friend WithEvents bRename As Button
End Class
