<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmSplash
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
        PictureBox1 = New PictureBox()
        Label1 = New Label()
        PictureBox2 = New PictureBox()
        Label2 = New Label()
        Label3 = New Label()
        Label4 = New Label()
        lblVersion = New Label()
        CType(PictureBox1, ComponentModel.ISupportInitialize).BeginInit()
        CType(PictureBox2, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' PictureBox1
        ' 
        PictureBox1.Image = My.Resources.Resources.FiKiIcon
        PictureBox1.Location = New Point(36, 39)
        PictureBox1.Name = "PictureBox1"
        PictureBox1.Size = New Size(58, 51)
        PictureBox1.TabIndex = 0
        PictureBox1.TabStop = False
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.BackColor = Color.Transparent
        Label1.Font = New Font("Segoe UI", 36F, FontStyle.Bold, GraphicsUnit.Point, 0)
        Label1.ForeColor = Color.White
        Label1.Location = New Point(126, 21)
        Label1.Name = "Label1"
        Label1.Size = New Size(325, 65)
        Label1.TabIndex = 2
        Label1.Text = "Findit6Agent"
        ' 
        ' PictureBox2
        ' 
        PictureBox2.Image = My.Resources.Resources.FiKiIcon
        PictureBox2.Location = New Point(474, 38)
        PictureBox2.Name = "PictureBox2"
        PictureBox2.Size = New Size(58, 50)
        PictureBox2.TabIndex = 3
        PictureBox2.TabStop = False
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.BackColor = Color.Transparent
        Label2.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0)
        Label2.ForeColor = Color.White
        Label2.Location = New Point(144, 79)
        Label2.Name = "Label2"
        Label2.Size = New Size(178, 17)
        Label2.TabIndex = 4
        Label2.Text = "Copyright: Michael Houben"
        ' 
        ' Label3
        ' 
        Label3.BackColor = Color.Transparent
        Label3.Font = New Font("Segoe UI", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0)
        Label3.ForeColor = Color.White
        Label3.Location = New Point(63, 108)
        Label3.Name = "Label3"
        Label3.Size = New Size(432, 144)
        Label3.TabIndex = 5
        Label3.TextAlign = ContentAlignment.MiddleCenter
        ' 
        ' Label4
        ' 
        Label4.AutoSize = True
        Label4.BackColor = Color.Transparent
        Label4.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0)
        Label4.ForeColor = Color.White
        Label4.Location = New Point(400, 79)
        Label4.Name = "Label4"
        Label4.Size = New Size(36, 17)
        Label4.TabIndex = 6
        Label4.Text = "2026"
        ' 
        ' lblVersion
        ' 
        lblVersion.AutoSize = True
        lblVersion.BackColor = Color.Transparent
        lblVersion.Font = New Font("Segoe UI", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0)
        lblVersion.ForeColor = Color.White
        lblVersion.Location = New Point(358, 79)
        lblVersion.Name = "lblVersion"
        lblVersion.Size = New Size(16, 17)
        lblVersion.TabIndex = 7
        lblVersion.Text = "B"
        ' 
        ' frmSplash
        ' 
        AutoScaleDimensions = New SizeF(7F, 17F)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = SystemColors.ControlDarkDark
        ClientSize = New Size(559, 288)
        ControlBox = False
        Controls.Add(lblVersion)
        Controls.Add(Label4)
        Controls.Add(Label3)
        Controls.Add(Label2)
        Controls.Add(Label1)
        Controls.Add(PictureBox2)
        Controls.Add(PictureBox1)
        FormBorderStyle = FormBorderStyle.FixedDialog
        Location = New Point(-500, -500)
        Name = "frmSplash"
        CType(PictureBox1, ComponentModel.ISupportInitialize).EndInit()
        CType(PictureBox2, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents PictureBox1 As PictureBox
    Friend WithEvents Label1 As Label
    Friend WithEvents PictureBox2 As PictureBox
    Friend WithEvents Label2 As Label
    Friend WithEvents Label3 As Label
    Friend WithEvents Label4 As Label
    Friend WithEvents lblVersion As Label
End Class
