<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmNaming
    Inherits System.Windows.Forms.Form

    'Das Formular überschreibt den Löschvorgang, um die Komponentenliste zu bereinigen.
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

    'Wird vom Windows Form-Designer benötigt.
    Private components As System.ComponentModel.IContainer

    'Hinweis: Die folgende Prozedur ist für den Windows Form-Designer erforderlich.
    'Das Bearbeiten ist mit dem Windows Form-Designer möglich.  
    'Das Bearbeiten mit dem Code-Editor ist nicht möglich.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        bOK = New Button()
        bCancel = New Button()
        txtName = New TextBox()
        SuspendLayout()
        ' 
        ' bOK
        ' 
        bOK.BackColor = SystemColors.ButtonFace
        bOK.FlatStyle = FlatStyle.Flat
        bOK.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        bOK.Location = New Point(306, 167)
        bOK.Margin = New Padding(5, 5, 5, 5)
        bOK.Name = "bOK"
        bOK.Size = New Size(187, 55)
        bOK.TabIndex = 10
        bOK.TabStop = False
        bOK.Text = "bOK"
        bOK.UseVisualStyleBackColor = False
        ' 
        ' bCancel
        ' 
        bCancel.BackColor = SystemColors.ButtonFace
        bCancel.DialogResult = DialogResult.Cancel
        bCancel.FlatStyle = FlatStyle.Flat
        bCancel.Font = New Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        bCancel.Location = New Point(115, 167)
        bCancel.Margin = New Padding(5, 5, 5, 5)
        bCancel.Name = "bCancel"
        bCancel.Size = New Size(181, 55)
        bCancel.TabIndex = 9
        bCancel.TabStop = False
        bCancel.Text = "bEsc"
        bCancel.UseVisualStyleBackColor = False
        ' 
        ' txtName
        ' 
        txtName.BorderStyle = BorderStyle.FixedSingle
        txtName.Font = New Font("Microsoft Sans Serif", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        txtName.HideSelection = False
        txtName.Location = New Point(14, 35)
        txtName.Margin = New Padding(4, 7, 4, 7)
        txtName.Name = "txtName"
        txtName.Size = New Size(478, 26)
        txtName.TabIndex = 1
        txtName.Text = "txtName"
        txtName.TextAlign = HorizontalAlignment.Center
        ' 
        ' frmNaming
        ' 
        AutoScaleDimensions = New SizeF(7F, 17F)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = SystemColors.AppWorkspace
        CancelButton = bCancel
        ClientSize = New Size(507, 245)
        Controls.Add(txtName)
        Controls.Add(bOK)
        Controls.Add(bCancel)
        FormBorderStyle = FormBorderStyle.FixedDialog
        Margin = New Padding(4, 4, 4, 4)
        MaximizeBox = False
        MinimizeBox = False
        Name = "frmNaming"
        ShowIcon = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        Text = "Form1"
        ResumeLayout(False)
        PerformLayout()

    End Sub

    Public WithEvents bOK As Button
    Public WithEvents bCancel As Button
    Friend WithEvents txtName As TextBox
End Class
