Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions

Module JsonProcessor

    Public Function TryExtractValidJson(ByVal input As String,
                                        ByRef jsonResult As String) As Boolean

        jsonResult = ""

        If String.IsNullOrWhiteSpace(input) Then
            Return False
        End If

        Dim cleaned As String = input.Trim()

        If TryValidateJsonCandidate(cleaned, jsonResult) Then
            Return True
        End If

        cleaned =
            Regex.Replace(
                cleaned,
                "^```(?:json)?\s*",
                "",
                RegexOptions.IgnoreCase)

        cleaned =
            Regex.Replace(
                cleaned,
                "\s*```$",
                "")

        cleaned = cleaned.Trim()

        If TryValidateJsonCandidate(cleaned, jsonResult) Then
            Return True
        End If

        Dim extracted As String =
            ExtractBalancedJsonObject(cleaned)

        If Not String.IsNullOrWhiteSpace(extracted) Then
            If TryValidateJsonCandidate(extracted, jsonResult) Then
                Return True
            End If
        End If

        LogDebug("No valid JSON found" & vbCrLf & extracted)
        Return False

    End Function

    Private Function TryValidateJsonCandidate(ByVal candidate As String,
                                              ByRef jsonResult As String) As Boolean

        If String.IsNullOrWhiteSpace(candidate) Then
            Return False
        End If

        candidate = NormalizeJsonWhitespace(candidate).Trim()

        If IsValidJson(candidate) Then
            jsonResult = candidate
            Return True
        End If

        Dim repaired As String =
            RepairJsonStringBackslashes(candidate)

        If IsValidJson(repaired) Then
            jsonResult = repaired.Trim()
            Return True
        End If

        Return False

    End Function

    Private Function NormalizeJsonWhitespace(ByVal rawJson As String) As String

        If String.IsNullOrEmpty(rawJson) Then
            Return rawJson
        End If

        Dim sb As New StringBuilder(rawJson.Length)
        Dim insideString As Boolean = False
        Dim escaped As Boolean = False

        For Each ch As Char In rawJson

            If insideString Then

                sb.Append(ch)

                If escaped Then
                    escaped = False
                ElseIf ch = "\"c Then
                    escaped = True
                ElseIf ch = """"c Then
                    insideString = False
                End If

                Continue For

            End If

            If ch = """"c Then
                insideString = True
                sb.Append(ch)
                Continue For
            End If

            Select Case AscW(ch)

                Case &HA0,
                     &H2000 To &H200A,
                     &H202F,
                     &H205F,
                     &H3000

                    sb.Append(" "c)

                Case &HFEFF
                    Continue For

                Case Else
                    sb.Append(ch)

            End Select

        Next

        Return sb.ToString()

    End Function

    Private Function RepairJsonStringBackslashes(ByVal rawJson As String) As String

        If String.IsNullOrEmpty(rawJson) Then
            Return rawJson
        End If

        Const fileMarker As String = "[file:///"

        Dim sb As New StringBuilder(rawJson.Length + 512)

        Dim insideString As Boolean = False
        Dim insideFileReference As Boolean = False

        Dim i As Integer = 0

        Do While i < rawJson.Length

            If insideString AndAlso
               Not insideFileReference AndAlso
               rawJson.IndexOf(fileMarker, i, StringComparison.OrdinalIgnoreCase) = i Then

                insideFileReference = True
                sb.Append(fileMarker)
                i += fileMarker.Length
                Continue Do

            End If

            Dim ch As Char = rawJson.Chars(i)

            If ch = """"c Then

                If Not IsEscapedByOddNumberOfBackslashes(rawJson, i) Then
                    insideString = Not insideString

                    If Not insideString Then
                        insideFileReference = False
                    End If
                End If

                sb.Append(ch)
                i += 1
                Continue Do

            End If

            If insideString AndAlso insideFileReference Then

                If ch = "]"c Then
                    insideFileReference = False
                    sb.Append(ch)
                    i += 1
                    Continue Do
                End If

                If ch = "\"c Then

                    If i + 1 < rawJson.Length AndAlso rawJson.Chars(i + 1) = "\"c Then
                        sb.Append("\\")
                        i += 2
                    Else
                        sb.Append("\\")
                        i += 1
                    End If

                    Continue Do

                End If

                sb.Append(ch)
                i += 1
                Continue Do

            End If

            If insideString AndAlso ch = "\"c Then

                If i + 1 >= rawJson.Length Then
                    sb.Append("\\")
                    i += 1
                    Continue Do
                End If

                Dim nextChar As Char = rawJson.Chars(i + 1)

                If IsValidJsonEscapeCharacter(nextChar) Then
                    sb.Append(ch)
                    sb.Append(nextChar)
                    i += 2
                Else
                    sb.Append("\\")
                    i += 1
                End If

                Continue Do

            End If

            sb.Append(ch)
            i += 1

        Loop

        Return sb.ToString()

    End Function

    Private Function IsValidJsonEscapeCharacter(ByVal ch As Char) As Boolean

        Select Case ch
            Case """"c, "\"c, "/"c, "b"c, "f"c, "n"c, "r"c, "t"c, "u"c
                Return True
        End Select

        Return False

    End Function

    Private Function IsEscapedByOddNumberOfBackslashes(ByVal text As String,
                                                    ByVal position As Integer) As Boolean

        Dim count As Integer = 0
        Dim i As Integer = position - 1

        Do While i >= 0 AndAlso text.Chars(i) = "\"c
            count += 1
            i -= 1
        Loop

        Return (count Mod 2) = 1

    End Function

    Private Function IsValidJson(ByVal text As String) As Boolean

        If String.IsNullOrWhiteSpace(text) Then
            Return False
        End If

        Try
            Using doc As JsonDocument = JsonDocument.Parse(text)
                Return True
            End Using

        Catch
            Return False
        End Try

    End Function

    Public Function ExtractBalancedJsonObject(ByVal text As String) As String

        If String.IsNullOrWhiteSpace(text) Then
            Return ""
        End If

        Dim startPos As Integer = -1
        Dim depth As Integer = 0
        Dim insideString As Boolean = False
        Dim escaped As Boolean = False

        For i As Integer = 0 To text.Length - 1

            Dim ch As Char = text.Chars(i)

            If insideString Then

                If escaped Then
                    escaped = False
                ElseIf ch = "\"c Then
                    escaped = True
                ElseIf ch = """"c Then
                    insideString = False
                End If

                Continue For

            End If

            If ch = """"c Then
                insideString = True
                Continue For
            End If

            If ch = "{"c Then

                If depth = 0 Then
                    startPos = i
                End If

                depth += 1

            ElseIf ch = "}"c Then

                depth -= 1

                If depth = 0 AndAlso startPos >= 0 Then
                    Return text.Substring(startPos, i - startPos + 1)
                End If

            End If

        Next

        Return ""

    End Function

End Module
