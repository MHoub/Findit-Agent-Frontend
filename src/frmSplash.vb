Public Class frmSplash
    Private ReadOnly SplashTimer As New Timer With {.Interval = 5000}

    Private Sub frmSplash_Shown(sender As Object, e As EventArgs) Handles Me.Shown

    End Sub

    Private Sub SplashTimer_Tick(sender As Object, e As EventArgs)
        SplashTimer.Stop()
        SplashTimer.Dispose()
        Me.Close()
    End Sub

    Private Sub frmSplash_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.BackColor = clr.Background
        lblVersion.BackColor = Me.BackColor
        lblVersion.Text = Version
        Label1.BackColor = Me.BackColor
        Label2.BackColor = Me.BackColor
        Label3.Text = "Findit6Agent is source-available under the PolyForm Perimeter License 1.0.1." & vbCrLf & vbCrLf & " Findit6a is free for private and experimental use; commercial use requires a licensed Findit"
        Me.Left = frmMain.Left + (frmMain.Width - Me.Width) \ 2
        Me.Top = frmMain.Top + (frmMain.Height - Me.Height) \ 2
        Me.TopMost = True
        Me.Refresh()
        Threading.Thread.Sleep(10)
        Me.Visible = True
        AddHandler SplashTimer.Tick, AddressOf SplashTimer_Tick
        SplashTimer.Start()
    End Sub

End Class