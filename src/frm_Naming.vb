Public Class frmNaming

    Public bNew As Boolean = False

    ' Small dialog used to assign a name to the currently selected path group.

    ' Initializes captions, colors and input focus.
    ' The title depends on whether this is the first group.
    Private Sub frmNaming_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        txtName.Multiline = False
        ' Load captions and dialog text.
        Me.AcceptButton = bOK
        If frm_Gruppen.cmbGruppen.Items.Count = 0 Then
            Me.Text = LetteringIni.ReadValue("frm_Treepfad", "lblGruppeNameFirst")
        Else
            Me.Text = LetteringIni.ReadValue("frm_Treepfad", "lblGruppeName")
        End If
        bOK.Text = LetteringIni.ReadValue("General", "bOK")
        bCancel.Text = LetteringIni.ReadValue("General", "bEsc")
        ' Apply the current color scheme.
        Me.BackColor = clr.Background
        bOK.BackColor = clr.Buttons
        bOK.ForeColor = clr.Text
        bCancel.BackColor = clr.Buttons
        bCancel.ForeColor = clr.Text
        txtName.BackColor = clr.Buttons
        txtName.ForeColor = clr.Text
        txtName.Text = ""
        SetDarkTitleBar(Me)
        Application.DoEvents()
        txtName.Focus()
        Application.DoEvents()
    End Sub

    ' Cancels the naming operation without changing group data.
    Private Sub bCancel_Click(sender As Object, e As EventArgs) Handles bCancel.Click
        bNew = False
        Me.Close()
        Me.Dispose()
    End Sub

    ' Validates the entered name and applies it to a new or existing group.
    ' Empty names and duplicate group names are rejected.
    Private Sub bOK_Click(sender As Object, e As EventArgs) Handles bOK.Click
        Dim neuerName As String = txtName.Text.Trim
        With frm_Gruppen.cmbGruppen
            ' Validate that a name was entered and that it is not already in use.
            If neuerName = "" Then
                MsgBoxNeu(LetteringIni.ReadValue("frm_Treepfad", "Name1"), MsgBoxStyle.OkOnly)
                txtName.Focus()
                Exit Sub
            ElseIf .Items.Cast(Of String).Any(Function(x) x = neuerName) Then
                MsgBoxNeu(LetteringIni.ReadValue("frm_Treepfad", "NameDoublette"), MsgBoxStyle.OkOnly)
                txtName.Focus()
                Exit Sub
            End If

            ' Apply the validated name.
            If bNew Then
                ' Store the name for a new group.
                frm_Gruppen.sNewGRPname = neuerName
            Else
                ' Rename the currently selected group.
                .Text = neuerName
                .Items(.SelectedIndex) = neuerName
                frm_Gruppen.aPathGroupList(.SelectedIndex).Name = neuerName
            End If
        End With
        Me.DialogResult = DialogResult.OK
        bNew = False
        Me.Close()
        Me.Dispose()
    End Sub

End Class