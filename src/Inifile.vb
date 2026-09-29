
Imports System.Text

Public Class IniFile
    Private path As String = ""
    Private useCache As Boolean = False
    Private cacheLoaded As Boolean = False
    Private cache As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.OrdinalIgnoreCase)

    ' Creates an INI wrapper for the specified file.
    ' Optional caching keeps parsed values in memory between reads.
    Public Sub New(filePath As String, Optional useCache As Boolean = False)
        Me.useCache = useCache
        If Not String.IsNullOrWhiteSpace(filePath) Then SetPath(filePath)
    End Sub

    ' Assigns a new INI file path.
    ' Any previously loaded cache is discarded when the path changes.
    Public Sub SetPath(filePath As String)
        path = filePath
        cacheLoaded = False
        cache.Clear()
    End Sub

    ' Loads the complete INI file into the case-insensitive cache.
    ' Blank lines and comment lines are ignored while parsing.
    Private Sub LoadCache()
        If cacheLoaded Then Exit Sub
        cache.Clear()
        Dim currentSection As String = ""

        For Each rawLine As String In IO.File.ReadAllLines(path, Encoding.UTF8)
            Dim line As String = rawLine.Trim()
            If line.Length > 0 AndAlso line(0) = ChrW(&HFEFF) Then line = line.Substring(1)
            If line = "" Then Continue For
            If line.StartsWith(";") Then Continue For
            If line.StartsWith("#") Then Continue For

            If line.StartsWith("[") AndAlso line.EndsWith("]") Then
                currentSection = line.Substring(1, line.Length - 2).Trim()
                If Not cache.ContainsKey(currentSection) Then cache.Add(currentSection, New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase))
                Continue For
            End If

            Dim p As Integer = line.IndexOf("="c)
            If p >= 0 AndAlso currentSection <> "" Then
                Dim key As String = line.Substring(0, p).Trim()
                Dim value As String = line.Substring(p + 1).Trim()
                If Not cache(currentSection).ContainsKey(key) Then cache(currentSection)(key) = value
            End If
        Next

        cacheLoaded = True
    End Sub

    ' Reads a value from the requested section and returns the default if it cannot be read.
    ' Cached and direct reads follow the same error and fallback behavior.
    Public Function ReadValue(section As String, key As String, Optional defaultValue As String = "") As String
        If String.IsNullOrWhiteSpace(path) Then Return defaultValue

        Try
            If useCache Then
                LoadCache()

                If cache.ContainsKey(section) AndAlso cache(section).ContainsKey(key) Then
                    Dim value As String = cache(section)(key)
                    If String.IsNullOrWhiteSpace(value) Then Return defaultValue
                    Return value
                End If

                Return defaultValue
            End If

            Dim currentSection As String = ""

            For Each rawLine As String In IO.File.ReadAllLines(path, Encoding.UTF8)
                Dim line As String = rawLine.Trim()
                If line.Length > 0 AndAlso line(0) = ChrW(&HFEFF) Then line = line.Substring(1)

                If line.StartsWith("[") AndAlso line.EndsWith("]") Then
                    currentSection = line.Substring(1, line.Length - 2).Trim()
                    Continue For
                End If

                If currentSection.Equals(section, StringComparison.OrdinalIgnoreCase) Then
                    Dim p As Integer = line.IndexOf("="c)

                    If p >= 0 Then
                        Dim currentKey As String = line.Substring(0, p).Trim()

                        If currentKey.Equals(key, StringComparison.OrdinalIgnoreCase) Then
                            Dim value As String = line.Substring(p + 1).Trim()
                            If String.IsNullOrWhiteSpace(value) Then Return defaultValue
                            Return value
                        End If
                    End If
                End If
            Next

        Catch ex As Exception
            LogDebug("!!!!!!!!!! ERROR: could not read INI file: " & path & " - " & ex.Message)
        End Try

        Return defaultValue
    End Function

    ' Writes or replaces one value in the requested section.
    ' Passing Nothing removes the key instead of writing a value.
    Public Sub WriteValue(section As String, key As String, value As String)

        If String.IsNullOrWhiteSpace(path) Then
            LogDebug("[ERROR] Cannot write INI value: file path is not set.")
            Return
        End If
        If value Is Nothing Then
            KillKey(section, key)
            Return
        End If
        value = value.Trim()

        Dim folder As String = IO.Path.GetDirectoryName(path)
        If Not String.IsNullOrWhiteSpace(folder) AndAlso Not IO.Directory.Exists(folder) Then IO.Directory.CreateDirectory(folder)

        Dim lines As New List(Of String)
        If IO.File.Exists(path) Then lines = New List(Of String)(IO.File.ReadAllLines(path, Encoding.UTF8))

        Dim inSection As Boolean = False

        For i As Integer = 0 To lines.Count - 1
            Dim line As String = lines(i).Trim()

            If line.StartsWith("[") AndAlso line.EndsWith("]") Then
                If inSection Then
                    Dim insertIndex As Integer = i
                    While insertIndex > 0 AndAlso String.IsNullOrWhiteSpace(lines(insertIndex - 1))
                        insertIndex -= 1
                    End While
                    lines.Insert(insertIndex, key & "=" & value)
                    SaveLines(lines)
                    UpdateCache(section, key, value)

                    Return
                End If

                Dim currentSection As String = line.Substring(1, line.Length - 2).Trim()
                inSection = currentSection.Equals(section, StringComparison.OrdinalIgnoreCase)

            ElseIf inSection Then
                Dim p As Integer = line.IndexOf("="c)
                If p >= 0 Then
                    Dim currentKey As String = line.Substring(0, p).Trim()
                    If currentKey.Equals(key, StringComparison.OrdinalIgnoreCase) Then
                        lines(i) = key & "=" & value
                        SaveLines(lines)
                        UpdateCache(section, key, value)
                        Return
                    End If
                End If
            End If
        Next

        If inSection Then
            lines.Add(key & "=" & value)
        Else
            lines.Add(key & "=" & value)
        End If

        SaveLines(lines)
        UpdateCache(section, key, value)
    End Sub

    ' Writes the complete in-memory line list back to the INI file.
    ' UTF-8 is used consistently for all file operations.
    Private Sub SaveLines(lines As List(Of String))
        IO.File.WriteAllLines(path, lines, Encoding.UTF8)
    End Sub

    ' Updates the in-memory cache after a successful write.
    ' No cache is created when caching has not yet been loaded.
    Private Sub UpdateCache(section As String, key As String, value As String)
        If Not cacheLoaded Then Exit Sub
        If Not cache.ContainsKey(section) Then cache.Add(section, New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase))
        cache(section)(key) = value
    End Sub

    ' Removes all occurrences of a key from the requested section.
    ' The loaded cache is updated to match the modified file.
    Public Sub KillKey(section As String, key As String)
        If String.IsNullOrWhiteSpace(path) Then
            LogDebug("[ERROR] Cannot remove INI key: file path is not set.")
            Return
        End If

        If Not IO.File.Exists(path) Then Return

        Dim lines As New List(Of String)(IO.File.ReadAllLines(path, Encoding.UTF8))
        Dim inSection As Boolean = False
        Dim removed As Boolean = False
        Dim i As Integer = 0

        While i < lines.Count
            Dim line As String = lines(i).Trim()
            If line.StartsWith("[") AndAlso line.EndsWith("]") Then
                Dim currentSection As String = line.Substring(1, line.Length - 2).Trim()
                inSection = currentSection.Equals(section, StringComparison.OrdinalIgnoreCase)
            ElseIf inSection Then
                Dim p As Integer = line.IndexOf("="c)
                If p >= 0 Then
                    Dim currentKey As String = line.Substring(0, p).Trim()

                    If currentKey.Equals(key, StringComparison.OrdinalIgnoreCase) Then
                        lines.RemoveAt(i)
                        removed = True
                        Continue While
                    End If
                End If
            End If
            i += 1
        End While
        If removed Then SaveLines(lines)
        If cacheLoaded AndAlso cache.ContainsKey(section) Then cache(section).Remove(key)
    End Sub

    ' Reads UI lettering from this INI file.
    ' The NZ marker is converted back into a line break.
    Public Function getLettering(section As String, key As String) As String
        Return ReadValue(section, key, "").Replace("NZ", vbCrLf)
    End Function


End Class