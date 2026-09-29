Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text


Module LittleHelpers

    <DllImport("dwmapi.dll")>
    Private Function DwmSetWindowAttribute(hwnd As IntPtr, attr As Integer, ByRef attrValue As Integer, attrSize As Integer) As Integer
    End Function

    Private Const DWMWA_USE_IMMERSIVE_DARK_MODE As Integer = 20
    Private Const DWMWA_USE_IMMERSIVE_DARK_MODE_OLD As Integer = 19

    ' Reads a named property from an object by reflection.
    ' Missing properties or reflection errors are treated as unavailable values.
    Public Function GetPropertyValue(obj As Object, propertyName As String) As Object
        Try
            Dim prop = obj.GetType().GetProperty(propertyName)
            If prop IsNot Nothing Then Return prop.GetValue(obj)
            Return Nothing
        Catch
            Return Nothing
        End Try
    End Function

    ' Escapes characters that have a special meaning inside RTF text.
    ' The returned string can be embedded safely in simple RTF fragments.
    Public Function RtfEscape(ByVal txt As String) As String
        Return txt.Replace("\", "\\").Replace("{", "\{").Replace("}", "\}")
    End Function

    ' Creates a short file-system-safe hash from a UTF path name.
    ' Twelve SHA-256 bytes are encoded using a URL-safe Base64 representation.
    Public Function HashPathName(ByVal sUTFPathName As String) As String
        Using sha As SHA256 = SHA256.Create()
            Dim bytes As Byte() = Encoding.UTF8.GetBytes(sUTFPathName)
            Dim hash As Byte() = sha.ComputeHash(bytes)
            Dim shortHash(11) As Byte
            Array.Copy(hash, shortHash, 12)

            Return Convert.ToBase64String(shortHash).TrimEnd("="c).Replace("+"c, "-"c).Replace("/"c, "_"c)
        End Using
    End Function

    ' Appends a timestamped message to the current session log.
    ' Logging failures are intentionally ignored so they never interrupt the application.
    Public Sub LogDebug(message As String)
        Try
            Dim logPath As String = IO.Path.Combine(SessionWorkspace, "Session.log")
            Dim logLine As String = DateTime.Now.ToString("HH:mm:ss.fff") & " | " & message & vbCrLf
            File.AppendAllText(logPath, logLine, Encoding.UTF8)
        Catch ex As Exception
            ' Logging must never interrupt normal application flow.
        End Try
    End Sub

    ' Persists the main window state and selected path-group metadata.
    ' Path details are maintained by the path-group workflow itself.
    Public Sub SaveAgentIni()
        AgentsIni.WriteValue("Main", "me.top", frmMain.Top.ToString)
        AgentsIni.WriteValue("Main", "me.left", frmMain.Left.ToString)
        AgentsIni.WriteValue("Main", "me.width", frmMain.Width.ToString)
        AgentsIni.WriteValue("Main", "me.height", frmMain.Height.ToString)
        AgentsIni.WriteValue("Main", "AgentWorkspace", AgentWorkspace)
        AgentsIni.WriteValue("Main", "sUTFpath", sUTFpath)
    End Sub

    ' Reads an integer value from AgentsIni and falls back to the supplied default.
    ' Invalid or manually damaged numeric values therefore do not prevent application startup.
    Private Function ReadAgentIniInteger(section As String, key As String, defaultValue As Integer) As Integer
        Dim value As Integer
        If Integer.TryParse(AgentsIni.ReadValue(section, key, defaultValue.ToString()), value) Then Return value
        Return defaultValue
    End Function

    ' Restores the main window state and the previously selected path group.
    ' Invalid numeric INI values are replaced with safe defaults.
    Public Sub LoadAgentIni()
        frmMain.Top = ReadAgentIniInteger("Main", "me.top", frmMain.Top)
        frmMain.Left = ReadAgentIniInteger("Main", "me.left", frmMain.Left)
        frmMain.Width = ReadAgentIniInteger("Main", "me.width", frmMain.Width)
        frmMain.Height = ReadAgentIniInteger("Main", "me.height", frmMain.Height)

        AgentWorkspace = AgentsIni.ReadValue("Main", "AgentWorkspace", "")
        sUTFpath = AgentsIni.ReadValue("Main", "sUTFpath", "")
        SelectedPathgroup = New PathGroup
        SelectedPathgroup.Number = ReadAgentIniInteger("LoadedPathGroup", "Number", 1)
        SelectedPathgroup.Name = AgentsIni.ReadValue("LoadedPathGroup", "Name", "")
        SelectedPathgroup.PathCount = Math.Max(0, ReadAgentIniInteger("LoadedPathGroup", "Pathcount", 0))
        For j As Integer = 0 To SelectedPathgroup.PathCount - 1
            Dim pathValue As String = AgentsIni.ReadValue("LoadedPathGroup", "Path" & (j + 1).ToString(), "")
            If Not String.IsNullOrWhiteSpace(pathValue) Then SelectedPathgroup.Paths.Add(pathValue)
        Next
        SelectedPathgroup.PathCount = SelectedPathgroup.Paths.Count
    End Sub

    ' Enables the immersive dark title bar on supported Windows versions.
    ' Older Windows builds are handled through the legacy attribute fallback.
    Public Sub SetDarkTitleBar(frm As Form)
        Try
            If frm Is Nothing OrElse frm.IsDisposed Then Exit Sub

            Dim useDark As Integer = 1
            Dim result As Integer = DwmSetWindowAttribute(frm.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, useDark, Marshal.SizeOf(Of Integer)())

            If result <> 0 Then
                DwmSetWindowAttribute(frm.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, useDark, Marshal.SizeOf(Of Integer)())
            End If
        Catch
            ' Unsupported systems should continue without dark title-bar support.
        End Try
    End Sub

    ' Reads a UI caption from the currently selected language file.
    ' NZ markers are converted back into line breaks.
    Public Function GetBeschriftung(section As String, key As String) As String
        Return LetteringIni.ReadValue(section, key, "").Replace("NZ", vbCrLf)
    End Function

End Module
