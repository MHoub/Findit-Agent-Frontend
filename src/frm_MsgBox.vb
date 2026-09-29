Imports System.Windows.Media.TextFormatting

' Provides a dark-mode replacement for the standard Windows message box.
' Supports standard MessageBoxButtons, custom button captions and VB-style MsgBoxStyle calls.
Public Class MHMsgBox
    Inherits System.Windows.Forms.Form

    Private mResult As DialogResult = DialogResult.Cancel

    Private lblMessage As Label
    Private lblTitle As Label
    Private picIcon As PictureBox
    Private pnlButtons As Panel
    Private btnButton1 As Button
    Private btnButton2 As Button
    Private btnButton3 As Button

    ' Initializes the dialog controls.
    ' Initialization errors are currently ignored to preserve the existing behavior.
    Public Sub New()
        InitializeComponent()
    End Sub

    ' Builds the basic message-box layout in code.
    ' Button controls are configured separately by the ShowDialog routines.
    Private Sub InitializeComponent()
        lblTitle = New Label()
        picIcon = New PictureBox()
        lblMessage = New Label()
        pnlButtons = New Panel()
        CType(picIcon, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 

        ' Buttons erstellen
        btnButton1 = CreateButton("")
        btnButton2 = CreateButton("")
        btnButton3 = CreateButton("")

        pnlButtons.Controls.Add(btnButton1)
        pnlButtons.Controls.Add(btnButton2)
        pnlButtons.Controls.Add(btnButton3)
        SetDarkTitleBar(Me)

        ' lblTitle
        ' 
        lblTitle.BackColor = Color.Transparent
        lblTitle.Font = New Font("Segoe UI", 11.0F, FontStyle.Bold)
        lblTitle.ForeColor = Color.White
        lblTitle.Location = New Point(15, 15)
        lblTitle.Name = "lblTitle"
        lblTitle.Size = New Size(420, 25)
        lblTitle.TabIndex = 0
        ' 
        ' picIcon
        ' 
        picIcon.BackColor = Color.Transparent
        picIcon.Location = New Point(15, 50)
        picIcon.Name = "picIcon"
        picIcon.Size = New Size(48, 48)
        picIcon.SizeMode = PictureBoxSizeMode.CenterImage
        picIcon.TabIndex = 1
        picIcon.TabStop = False
        ' 
        ' lblMessage
        ' 
        lblMessage.BackColor = Color.Transparent
        lblMessage.Font = New Font("Segoe UI", 10.0F)
        lblMessage.ForeColor = Color.White
        lblMessage.Location = New Point(75, 50)
        lblMessage.Name = "lblMessage"
        lblMessage.Size = New Size(355, 80)
        lblMessage.TabIndex = 2
        ' 
        ' pnlButtons
        ' 
        pnlButtons.BackColor = Color.FromArgb(CByte(74), CByte(81), CByte(89))
        pnlButtons.Location = New Point(0, 140)
        pnlButtons.Name = "pnlButtons"
        pnlButtons.Size = New Size(500, 60)
        pnlButtons.TabIndex = 3
        ' 
        ' MHMsgBox
        ' 
        BackColor = Color.FromArgb(CByte(98), CByte(105), CByte(115))
        ClientSize = New Size(484, 161)
        Controls.Add(lblTitle)
        Controls.Add(picIcon)
        Controls.Add(lblMessage)
        Controls.Add(pnlButtons)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "MHMsgBox"
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterScreen
        TopMost = True
        CType(picIcon, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
    End Sub


    Public Function MsgBoxNeu(message As String,
                          Optional buttons As MessageBoxButtons = MessageBoxButtons.OK,
                          Optional icon As MessageBoxIcon = MessageBoxIcon.None,
                          Optional title As String = "FINDIT6Agent") As DialogResult

        Try
            Using frm As New MHMsgBox()
                Return frm.ShowDialog(message, buttons, icon, title)
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.ToString())
            Return DialogResult.OK
        End Try

    End Function

    ' Creates a consistently styled dark-mode button.
    ' The caller decides its text, visibility and final position.
    Private Function CreateButton(text As String) As Button
        Dim btn As New Button()

        btn.Size = New Size(115, 32)
        btn.Font = New Font("Segoe UI", 10)
        btn.FlatStyle = FlatStyle.Flat
        btn.BackColor = clr.Buttons
        btn.ForeColor = clr.Text
        btn.FlatAppearance.BorderColor = Color.White
        btn.FlatAppearance.BorderSize = 1
        btn.FlatAppearance.MouseOverBackColor = Color.White
        btn.Text = text
        btn.Visible = False

        Return btn
    End Function

    ' Shows the dialog with standard MessageBoxButtons and MessageBoxIcon values.
    ' The message area and form height are adjusted to the supplied text.
    Public Shadows Function ShowDialog(message As String, buttons As MessageBoxButtons, icon As MessageBoxIcon, title As String) As DialogResult
        Me.Text = title
        lblTitle.Text = title
        lblMessage.Text = message

        Dim fontName As New System.Drawing.Font("Segoe UI", 10, FontStyle.Regular)
        lblMessage.Font = fontName

        Dim messageSize As Size = TextRenderer.MeasureText(message, lblMessage.Font, New Size(355, Integer.MaxValue), TextFormatFlags.WordBreak)
        lblMessage.Height = messageSize.Height + 20

        Dim neededHeight As Integer = 40 + lblMessage.Top + lblMessage.Height + 60 + 30
        Me.Height = Math.Max(neededHeight, 200)

        pnlButtons.Top = Me.ClientSize.Height - 60
        pnlButtons.Height = 60

        SetIcon(icon)
        ConfigureButtons(buttons)

        Me.Left = 10
        Me.Top = 10

        MyBase.ShowDialog()
        Return mResult
    End Function

    ' Shows the dialog with one to three custom button captions.
    ' Custom button positions and result values are configured automatically.
    Public Shadows Function ShowDialog(message As String, button1Text As String, Optional button2Text As String = Nothing, Optional button3Text As String = Nothing, Optional icon As MessageBoxIcon = MessageBoxIcon.None, Optional title As String = "FINDIT6Agent") As DialogResult
        Me.Text = title
        lblTitle.Text = title
        lblMessage.Text = message

        Dim fontName As New System.Drawing.Font("Segoe UI", 10, FontStyle.Regular)
        lblMessage.Font = fontName

        Dim messageSize As Size = TextRenderer.MeasureText(message, lblMessage.Font, New Size(355, Integer.MaxValue), TextFormatFlags.WordBreak)
        lblMessage.Height = messageSize.Height + 20

        Dim neededHeight As Integer = 40 + lblMessage.Top + lblMessage.Height + 60 + 30
        Me.Height = Math.Max(neededHeight, 200)

        pnlButtons.Top = Me.ClientSize.Height - 60
        pnlButtons.Height = 60

        SetIcon(icon)
        ConfigureCustomButtons(button1Text, button2Text, button3Text)

        Me.Left = 10
        Me.Top = 10

        MyBase.ShowDialog()
        Return mResult
    End Function

    ' Applies the requested system icon to the dialog.
    ' Without an icon, the message text expands into the icon area.
    Private Sub SetIcon(icon As MessageBoxIcon)
        Select Case icon
            Case MessageBoxIcon.Information
                picIcon.Image = SystemIcons.Information.ToBitmap()

            Case MessageBoxIcon.Question
                picIcon.Image = SystemIcons.Question.ToBitmap()

            Case MessageBoxIcon.Warning
                picIcon.Image = SystemIcons.Warning.ToBitmap()

            Case MessageBoxIcon.Error
                picIcon.Image = SystemIcons.Error.ToBitmap()

            Case Else
                picIcon.Visible = False
                lblMessage.Left = 15
                lblMessage.Width = 415
        End Select
    End Sub

    ' Configures standard OK, Cancel, Yes and No button combinations.
    ' Each visible button closes the dialog and stores its corresponding DialogResult.
    Private Sub ConfigureButtons(buttons As MessageBoxButtons)
        btnButton1.Visible = False
        btnButton2.Visible = False
        btnButton3.Visible = False

        Select Case buttons
            Case MessageBoxButtons.OK
                btnButton1.Text = "OK"
                btnButton1.Visible = True
                btnButton1.Location = New Point((pnlButtons.Width - 100) \ 2, 14)

                AddHandler btnButton1.Click,
                    Sub()
                        mResult = DialogResult.OK
                        Me.Close()
                    End Sub

                Me.AcceptButton = btnButton1

            Case MessageBoxButtons.OKCancel
                btnButton1.Text = "OK"
                btnButton2.Text = "Abbrechen"
                btnButton1.Visible = True
                btnButton2.Visible = True
                btnButton1.Location = New Point((pnlButtons.Width - 220) \ 2, 14)
                btnButton2.Location = New Point(btnButton1.Right + 20, 14)

                AddHandler btnButton1.Click,
                    Sub()
                        mResult = DialogResult.OK
                        Me.Close()
                    End Sub

                AddHandler btnButton2.Click,
                    Sub()
                        mResult = DialogResult.Cancel
                        Me.Close()
                    End Sub

                Me.AcceptButton = btnButton1
                Me.CancelButton = btnButton2

            Case MessageBoxButtons.YesNo
                btnButton1.Text = "Ja"
                btnButton2.Text = "Nein"
                btnButton1.Visible = True
                btnButton2.Visible = True
                btnButton1.Location = New Point((pnlButtons.Width - 220) \ 2, 14)
                btnButton2.Location = New Point(btnButton1.Right + 20, 14)

                AddHandler btnButton1.Click,
                    Sub()
                        mResult = DialogResult.Yes
                        Me.Close()
                    End Sub

                AddHandler btnButton2.Click,
                    Sub()
                        mResult = DialogResult.No
                        Me.Close()
                    End Sub

                Me.AcceptButton = btnButton1

            Case MessageBoxButtons.YesNoCancel
                btnButton1.Text = "Ja"
                btnButton2.Text = "Nein"
                btnButton3.Text = "Abbrechen"
                btnButton1.Visible = True
                btnButton2.Visible = True
                btnButton3.Visible = True
                btnButton1.Location = New Point((pnlButtons.Width - 340) \ 2, 14)
                btnButton2.Location = New Point(btnButton1.Right + 20, 14)
                btnButton3.Location = New Point(btnButton2.Right + 20, 14)

                AddHandler btnButton1.Click,
                    Sub()
                        mResult = DialogResult.Yes
                        Me.Close()
                    End Sub

                AddHandler btnButton2.Click,
                    Sub()
                        mResult = DialogResult.No
                        Me.Close()
                    End Sub

                AddHandler btnButton3.Click,
                    Sub()
                        mResult = DialogResult.Cancel
                        Me.Close()
                    End Sub

                Me.AcceptButton = btnButton1
                Me.CancelButton = btnButton3
        End Select
    End Sub

    ' Configures one to three buttons with caller-defined captions.
    ' Button 1 maps to Yes, button 2 to No and button 3 to Cancel.
    Private Sub ConfigureCustomButtons(button1Text As String, Optional button2Text As String = Nothing, Optional button3Text As String = Nothing)
        btnButton1.Visible = False
        btnButton2.Visible = False
        btnButton3.Visible = False

        RemoveHandler btnButton1.Click, Nothing
        RemoveHandler btnButton2.Click, Nothing
        RemoveHandler btnButton3.Click, Nothing

        btnButton1.Text = button1Text
        btnButton1.Visible = True

        Dim hasButton2 As Boolean = Not String.IsNullOrEmpty(button2Text)
        If hasButton2 Then
            btnButton2.Text = button2Text
            btnButton2.Visible = True
        End If

        Dim hasButton3 As Boolean = Not String.IsNullOrEmpty(button3Text)
        If hasButton3 Then
            btnButton3.Text = button3Text
            btnButton3.Visible = True
        End If

        AdjustButtonWidth(btnButton1)
        If hasButton2 Then AdjustButtonWidth(btnButton2)
        If hasButton3 Then AdjustButtonWidth(btnButton3)

        If hasButton3 Then
            Dim totalWidth As Integer = btnButton1.Width + btnButton2.Width + btnButton3.Width + 40
            btnButton1.Location = New Point((pnlButtons.Width - totalWidth) \ 2, 14)
            btnButton2.Location = New Point(btnButton1.Right + 20, 14)
            btnButton3.Location = New Point(btnButton2.Right + 20, 14)
        ElseIf hasButton2 Then
            Dim totalWidth As Integer = btnButton1.Width + btnButton2.Width + 20
            btnButton1.Location = New Point((pnlButtons.Width - totalWidth) \ 2, 14)
            btnButton2.Location = New Point(btnButton1.Right + 20, 14)
        Else
            btnButton1.Location = New Point((pnlButtons.Width - btnButton1.Width) \ 2, 14)
        End If

        AddHandler btnButton1.Click,
            Sub()
                mResult = DialogResult.Yes
                Me.Close()
            End Sub

        If hasButton2 Then
            AddHandler btnButton2.Click,
                Sub()
                    mResult = DialogResult.No
                    Me.Close()
                End Sub
        End If

        If hasButton3 Then
            AddHandler btnButton3.Click,
                Sub()
                    mResult = DialogResult.Cancel
                    Me.Close()
                End Sub
        End If

        Me.AcceptButton = btnButton1

        If hasButton3 Then
            Me.CancelButton = btnButton3
        ElseIf hasButton2 Then
            Me.CancelButton = btnButton2
        End If
    End Sub

    ' Adjusts a custom button width to its caption.
    ' Width is limited to a practical range between 100 and 200 pixels.
    Private Sub AdjustButtonWidth(btn As Button)
        Dim textWidth As Integer = TextRenderer.MeasureText(btn.Text, btn.Font).Width + 30
        btn.Width = Math.Max(100, Math.Min(200, textWidth))
    End Sub

    ' Shows the form without additional message-box configuration.
    ' Returns the result stored by the active button handler.
    Public Shadows Function ShowDialog() As DialogResult
        MyBase.ShowDialog()
        Return mResult
    End Function

    ' Applies the dark title-bar styling when the form is loaded.
    Private Sub MHMsgBox_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        SetDarkTitleBar(Me)
    End Sub

End Class


' Provides application-wide helper overloads for the dark-mode message box.
' Callers can use standard, custom-caption or VB-style button definitions.
Module MsgBoxNeuHelper

    ' Shows the custom dialog with standard MessageBoxButtons values.
    ' Any unexpected dialog error currently falls back to DialogResult.OK.
    Public Function MsgBoxNeu(message As String, Optional buttons As MessageBoxButtons = MessageBoxButtons.OK, Optional icon As MessageBoxIcon = MessageBoxIcon.None, Optional title As String = "FINDIT6Agent") As DialogResult
        Try
            Using frm As New MHMsgBox()
                Return frm.ShowDialog(message, buttons, icon, title)
            End Using
        Catch
            Return DialogResult.OK
        End Try
    End Function

    ' Shows the custom dialog with one to three caller-defined button captions.
    ' Any unexpected dialog error currently falls back to DialogResult.OK.
    Public Function MsgBoxNeu(message As String, button1Text As String, Optional button2Text As String = Nothing, Optional button3Text As String = Nothing, Optional icon As MessageBoxIcon = MessageBoxIcon.None, Optional title As String = "FINDIT6Agent") As DialogResult
        Try
            Using frm As New MHMsgBox()
                Return frm.ShowDialog(message, button1Text, button2Text, button3Text, icon, title)
            End Using
        Catch
            Return DialogResult.OK
        End Try
    End Function

    ' Provides compatibility with existing VB-style MsgBoxStyle calls.
    ' Button and icon flags are converted to their WinForms equivalents.
    Public Function MsgBoxNeu(message As String, buttons As MsgBoxStyle, Optional title As String = "FINDIT6ASgent") As MsgBoxResult
        Try
            Dim mbButtons As MessageBoxButtons = MessageBoxButtons.OK
            Dim mbIcon As MessageBoxIcon = MessageBoxIcon.None

            If (buttons And MsgBoxStyle.OkCancel) = MsgBoxStyle.OkCancel Then
                mbButtons = MessageBoxButtons.OKCancel
            ElseIf (buttons And MsgBoxStyle.YesNo) = MsgBoxStyle.YesNo Then
                mbButtons = MessageBoxButtons.YesNo
            ElseIf (buttons And MsgBoxStyle.YesNoCancel) = MsgBoxStyle.YesNoCancel Then
                mbButtons = MessageBoxButtons.YesNoCancel
            End If

            If (buttons And MsgBoxStyle.Information) = MsgBoxStyle.Information Then
                mbIcon = MessageBoxIcon.Information
            ElseIf (buttons And MsgBoxStyle.Question) = MsgBoxStyle.Question Then
                mbIcon = MessageBoxIcon.Question
            ElseIf (buttons And MsgBoxStyle.Exclamation) = MsgBoxStyle.Exclamation Then
                mbIcon = MessageBoxIcon.Warning
            ElseIf (buttons And MsgBoxStyle.Critical) = MsgBoxStyle.Critical Then
                mbIcon = MessageBoxIcon.Error
            End If

            Dim result As DialogResult

            Using frm As New MHMsgBox()
                result = frm.ShowDialog(message, mbButtons, mbIcon, title)
            End Using

            Select Case result
                Case DialogResult.OK
                    Return MsgBoxResult.Ok

                Case DialogResult.Cancel
                    Return MsgBoxResult.Cancel

                Case DialogResult.Yes
                    Return MsgBoxResult.Yes

                Case DialogResult.No
                    Return MsgBoxResult.No

                Case Else
                    Return MsgBoxResult.Ok
            End Select
        Catch
            Return MsgBoxResult.Ok
        End Try
    End Function

End Module