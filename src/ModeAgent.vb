Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Timers

Module ModeAgent
    ' Agent Mode runtime: orchestrates AI responses, Findit6 searches, timeout recovery, file delivery and prompt construction.
    ' Provider-specific transport is handled elsewhere; this module operates on the shared request/response state.
    ' Workflow steps stay deliberately small so individual actions can be debugged without tracing the complete agent loop.

    Private Const RecoveryGraceSeconds As Integer = 90
    Private sTooManyHitsReportedFor As String = ""

#Region "Agent workflow"

    ' Central timer-driven coordinator for Agent Mode.
    ' Processes AI responses, checks timeout recovery, monitors Findit6 and sends any prepared prompt.
    ' Returns optional status or result text for the user interface.
    Public Function agentworkflow() As String
        Dim sMsgToUser As String = ""
        ' Process any response already received from the AI provider.
        Dim responseMessage As String = ProcessIncomingAgentResponse()
        If Not String.IsNullOrWhiteSpace(responseMessage) Then sMsgToUser = responseMessage
        ' Check whether an unanswered prompt requires timeout recovery.
        ' Timeout recovery only for providers with persistent/managed sessions.
        If Not TypeOf currentProvider Is OpenAICompatibleProvider Then
            CheckAgentTimeoutReminder()
        End If
        ' Monitor a running Findit6 search and prepare status/result prompts.
        Dim finditMessage As String = MonitorAgentFindit()
        If Not String.IsNullOrWhiteSpace(finditMessage) Then sMsgToUser = finditMessage
        ' Send any prompt prepared by the workflow.
        SendPendingAgentPrompt()
        ' Return control to the timer and optionally pass a message to the UI.
        Return sMsgToUser
    End Function

    ' Processes one incoming Agent response and dispatches valid JSON actions.
    ' Invalid JSON is handled internally and is not shown to the user.
    Private Function ProcessIncomingAgentResponse() As String
        If String.IsNullOrWhiteSpace(KIresponse) Then Return ""

        Dim rawResponse As String = KIresponse
        Dim consumeResponse As Boolean = True

        Try
            Using jsonDocument As JsonDocument = JsonDocument.Parse(rawResponse)
                Dim jResponse As JsonElement = jsonDocument.RootElement
                Dim action As String = jResponse.GetProperty("action").GetString().ToLowerInvariant()
                sLastAgentResponseType = action
                LogDebug("<<<< Received action: " & action)
                Select Case action.ToLowerInvariant()
                    Case "answer", "synthesis_ready"
                        query.bAgentRunning = False
                    Case Else
                        query.bAgentRunning = True
                End Select
                If action = "answer" Or action = "Answer" Then
                    Dim jAnswer As JsonElement
                    If jResponse.TryGetProperty("answer", jAnswer) Then
                        Dim sAnswer As String = If(jAnswer.GetString(), "")
                        If sAnswer.StartsWith("This session has reached the maximum context size allowed by the AI provider.", StringComparison.OrdinalIgnoreCase) AndAlso
                             sAnswer.IndexOf("Please start a new session to continue working.", StringComparison.OrdinalIgnoreCase) >= 0 Then
                            bContextLimit = True
                        End If
                        If sAnswer.Length < 1000 Then LogDebug("<<<< Short answer: " & sAnswer)
                    End If
                End If

                Dim result As String = ProcessAgentAction(jResponse, action, consumeResponse)
                If consumeResponse Then KIresponse = ""
                Return result
            End Using

        Catch ex As Exception
            If bInvalidJson Then
                LogDebug("INVALID Json: " & rawResponse)
                HandleJsonParseError()
                bInvalidJson = False
                KIresponse = ""
                Return "Agent produced invalid json - was warned, will try again!"
            End If

            Dim sMsgToUser As String = "Unknown Error with KIresponse: " & rawResponse & vbCrLf & "Errormessage: " & ex.Message & vbCrLf & ex.StackTrace
            LogDebug(sMsgToUser)
            KIresponse = ""
            Return sMsgToUser
        End Try
    End Function

    ' Dispatches one validated agent action to the corresponding handler.
    ' consumeResponse=False keeps KIresponse for a later timer cycle, currently used while the file worker is busy.
    ' Returns optional text for the user interface
    Private Function ProcessAgentAction(jResponse As JsonElement, action As String, ByRef consumeResponse As Boolean) As String
        If iWaitingForLedger > 0 AndAlso Not action.Equals("ledgerentries", StringComparison.OrdinalIgnoreCase) Then
            consumeResponse = True
            sPromptToProcess = " MANDATORY NEXT ACTION: ledgerentries. This is the ONLY permitted action now. Return one Ledger entry for EVERY file in the complete last Context Excerpt list. Do not skip, merge or summarize files. Do NOT request or read files, do NOT search, do NOT send working, and do NOT answer the user. No other action is permitted until the complete Ledger has been received. Any other action will be ignored and this instruction will be repeated."
            Return ""
        End If

        Select Case action
            Case "ledgerentries"
                iWaitingForLedger = 0
                Return ProcessLedgerEntries(jResponse)

            Case "maths", "Maths"
                ProcessMaths(jResponse)
                Return ""

            Case "requestledger"
                Return ProcessRequestLedger(jResponse)

            Case "researchmode"
                Return ProcessAction_ResearchMode(jResponse)

            Case "fullsearch", "hitlistsearch"

                If Not ProcessAction_SearchStrategy(jResponse, action) Then
                    Return ""
                End If

                Return ProcessAction_Search(jResponse, action)

            Case "waiting"
                Return ""

            Case "working"
                sPromptToProcess = "Continue your current analysis. Do not repeat the progress report. Proceed until you can send the next required action or the final result."
                Return ""

            Case "request_file"
                If bworkerbusy Then
                    consumeResponse = False
                    Return ""
                End If

                Try
                    ProcessAction_RequestFile(jResponse)
                    Return ""
                Catch ex As Exception
                    LogDebug("Problem processing file request: " & ex.Message)
                    Return "Problem processing file request"
                End Try

            Case "synthesisready"
                frmMain.Spinner.Visible = False
                query.bAgentRunning = False
                Return ShowSynthesis(jResponse)

            Case "answer"
                LogDebug("<<<< ANSWER RAW: " & jResponse.GetRawText())
                Dim jAnswer As JsonElement
                Dim sAnswer As String = ""
                If jResponse.TryGetProperty("text", jAnswer) Then
                    sAnswer = jAnswer.GetString()
                ElseIf jResponse.TryGetProperty("answer", jAnswer) Then
                    sAnswer = jAnswer.GetString()
                    LogDebug("<<<< Answer used fallback field: answer")
                ElseIf jResponse.TryGetProperty("Answer", jAnswer) Then
                    sAnswer = jAnswer.GetString()
                    LogDebug("<<<< Answer used fallback field: Answer")
                Else
                    LogDebug("<<<< Invalid answer JSON: no text/answer/Answer field")
                    sPromptToProcess = "Your response used action 'answer', but no answer text was found. Please repeat your response using exactly: {""action"":""answer"",""text"":""<your answer>""}"
                    Return ""
                End If

                LogDebug("<<<< Short answer: " & sAnswer)
                Return sAnswer

            Case Else
                LogDebug("<<<< UNKNOWN ACTION: '" & action & "'")
                sLastAgentResponseType = "unknown action"
                sPromptToProcess = "Valid JSON, but unknown action: '" & action & "'. Please use one of these: researchmode, searchstrategy, fullsearch, hitlistsearch, answer, waiting, request_file, working, synthesisready. Repeat your response in correct syntax!"
                Return ""
        End Select
    End Function
#End Region

#Region "Ledger & Maths"
    Private Sub ProcessMaths(ByVal jResponse As JsonElement)

        Dim jOperation As JsonElement
        Dim jValues As JsonElement

        If Not jResponse.TryGetProperty("operation", jOperation) OrElse jOperation.ValueKind <> JsonValueKind.String OrElse String.IsNullOrWhiteSpace(jOperation.GetString()) Then
            sPromptToProcess = "Your Maths request was rejected because the required field 'operation' is missing or empty. Send the complete Maths request again."
            LogDebug("Maths rejected: operation missing")
            Exit Sub
        End If

        If Not jResponse.TryGetProperty("values", jValues) OrElse jValues.ValueKind <> JsonValueKind.Array OrElse jValues.GetArrayLength() = 0 Then
            sPromptToProcess = "Your Maths request was rejected because the required field 'values' is missing, is not an array, or contains no values. Send the complete Maths request again."
            LogDebug("Maths rejected: values missing or empty")
            Exit Sub
        End If

        Dim sOperation As String = jOperation.GetString().Trim().ToLowerInvariant()
        Dim oValues As New List(Of Decimal)
        Dim oOriginalValues As New List(Of String)

        For Each jValue As JsonElement In jValues.EnumerateArray()

            Dim sValue As String = ""

            If jValue.ValueKind = JsonValueKind.String Then
                sValue = jValue.GetString().Trim()
            ElseIf jValue.ValueKind = JsonValueKind.Number Then
                sValue = jValue.GetRawText()
            Else
                sPromptToProcess = "Your Maths request was rejected because every item in 'values' must be a numerical value. Send the complete Maths request again."
                LogDebug("Maths rejected: invalid value type")
                Exit Sub
            End If

            Dim dValue As Decimal

            If Not Decimal.TryParse(sValue, NumberStyles.Number, CultureInfo.InvariantCulture, dValue) Then
                sPromptToProcess = "Your Maths request was rejected because '" & sValue & "' is not a valid numerical value. Use a decimal point as decimal separator and send the complete Maths request again."
                LogDebug("Maths rejected: invalid number " & sValue)
                Exit Sub
            End If

            oOriginalValues.Add(sValue)
            oValues.Add(dValue)
        Next

        Dim dResult As Decimal

        Select Case sOperation

            Case "addition"
                dResult = 0D
                For Each dValue As Decimal In oValues
                    dResult += dValue
                Next

            Case "subtraction"
                dResult = oValues(0)
                For i As Integer = 1 To oValues.Count - 1
                    dResult -= oValues(i)
                Next

            Case "multiplication"
                dResult = 1D
                For Each dValue As Decimal In oValues
                    dResult *= dValue
                Next

            Case "division"
                dResult = oValues(0)
                For i As Integer = 1 To oValues.Count - 1
                    If oValues(i) = 0D Then
                        sPromptToProcess = "Your Maths request was rejected because division by zero is not possible. Correct the values and send the Maths request again."
                        LogDebug("Maths rejected: division by zero")
                        Exit Sub
                    End If
                    dResult /= oValues(i)
                Next

            Case Else
                sPromptToProcess = "Your Maths request was rejected because operation '" & sOperation & "' is unknown. Allowed operations are addition, subtraction, multiplication and division. Send the complete Maths request again."
                LogDebug("Maths rejected: unknown operation " & sOperation)
                Exit Sub

        End Select

        Dim sResult As String = dResult.ToString(CultureInfo.InvariantCulture)

        LogDebug("Maths: " & sOperation & " | " & String.Join(" / ", oOriginalValues) & " | Result: " & sResult)

        sPromptToProcess = "The result of the " & sOperation & " you requested for " & oValues.Count.ToString() & " values is " & sResult & ". Use this exact calculated result for this operation."

    End Sub

    Private Function ProcessLedgerEntries(ByVal jResponse As JsonElement) As String

        Dim sSchemaError As String = ""
        If Not ValidateLedgerEntriesJson(jResponse, sSchemaError) Then
            LogDebug("LedgerEntries rejected: " & sSchemaError)

            SendLedgerEntriesCorrectionPrompt(sSchemaError)

            Return ""
        End If

        Dim jLedgerName As JsonElement
        If Not jResponse.TryGetProperty("ledger_name", jLedgerName) OrElse jLedgerName.ValueKind <> JsonValueKind.String Then Return "LedgerEntries rejected: ledger_name is missing."


        Dim sLedgerName As String = If(jLedgerName.GetString(), "").Trim()
        If sLedgerName.Length = 0 Then Return "LedgerEntries rejected: ledger_name is empty."

        Dim oLedger As TAgentLedger = Nothing

        If Not query.Ledgers.TryGetValue(sLedgerName, oLedger) Then
            oLedger = New TAgentLedger With {.Name = sLedgerName}
            query.Ledgers.Add(sLedgerName, oLedger)
        End If

        Dim jPurpose As JsonElement
        If String.IsNullOrWhiteSpace(oLedger.Purpose) AndAlso jResponse.TryGetProperty("purpose", jPurpose) AndAlso jPurpose.ValueKind = JsonValueKind.String Then oLedger.Purpose = If(jPurpose.GetString(), "").Trim()

        Dim jResultUnit As JsonElement
        If String.IsNullOrWhiteSpace(oLedger.ResultUnit) AndAlso jResponse.TryGetProperty("result_unit", jResultUnit) AndAlso jResultUnit.ValueKind = JsonValueKind.String Then oLedger.ResultUnit = If(jResultUnit.GetString(), "").Trim()

        Dim jEntries As JsonElement
        If Not jResponse.TryGetProperty("entries", jEntries) OrElse jEntries.ValueKind <> JsonValueKind.Array Then Return "LedgerEntries rejected: entries array is missing."

        Dim iReceived As Integer = 0
        Dim iAdded As Integer = 0
        Dim iUpdated As Integer = 0
        Dim iIgnored As Integer = 0

        For Each jEntry As JsonElement In jEntries.EnumerateArray()

            iReceived += 1

            If jEntry.ValueKind <> JsonValueKind.Object Then
                iIgnored += 1
                Continue For
            End If

            Dim jFilePath As JsonElement
            If Not jEntry.TryGetProperty("file_path", jFilePath) OrElse jFilePath.ValueKind <> JsonValueKind.String Then
                iIgnored += 1
                Continue For
            End If

            Dim sFilePath As String = If(jFilePath.GetString(), "").Trim()
            If sFilePath.Length = 0 Then
                iIgnored += 1
                Continue For
            End If

            Dim sFacts As String = ""
            Dim jFacts As JsonElement
            If jEntry.TryGetProperty("facts", jFacts) AndAlso jFacts.ValueKind = JsonValueKind.String Then sFacts = If(jFacts.GetString(), "").Trim()

            Dim dFileDate As DateTime? = Nothing
            Dim jFileDate As JsonElement

            If jEntry.TryGetProperty("file_date", jFileDate) AndAlso jFileDate.ValueKind = JsonValueKind.String Then
                Dim dParsed As DateTime
                If DateTime.TryParse(If(jFileDate.GetString(), ""), dParsed) Then dFileDate = dParsed
            End If

            Dim oEntry As TAgentLedgerEntry = Nothing

            If query.Ledgers(sLedgerName).Entries.TryGetValue(sFilePath, oEntry) Then

                If Not oEntry.FileDate.HasValue AndAlso dFileDate.HasValue Then oEntry.FileDate = dFileDate

                If sFacts.Length > 0 Then
                    Dim bAlreadyStored As Boolean = oEntry.Facts.Split(New String() {" --/-- "}, StringSplitOptions.None).Any(Function(s) String.Equals(s.Trim(), sFacts, StringComparison.OrdinalIgnoreCase))

                    If Not bAlreadyStored Then
                        If oEntry.Facts.Length > 0 Then oEntry.Facts &= " --/-- "
                        oEntry.Facts &= sFacts
                        iUpdated += 1
                    End If
                End If

            Else

                oEntry = New TAgentLedgerEntry With {.FilePath = sFilePath, .FileDate = dFileDate, .Facts = sFacts}
                query.Ledgers(sLedgerName).Entries.Add(sFilePath, oEntry)
                iAdded += 1

            End If

        Next
        LogDebug("--------------> LEDGER ENTRIES")
        LogDebug("Ledger:              " & sLedgerName)
        LogDebug("Purpose:             " & oLedger.Purpose)
        LogDebug("Result unit:         " & oLedger.ResultUnit)
        LogDebug("Received entries:    " & iReceived.ToString())
        LogDebug("New files:           " & iAdded.ToString())
        LogDebug("Updated files:       " & iUpdated.ToString())
        LogDebug("Ignored entries:     " & iIgnored.ToString())
        LogDebug("Ledger total files:  " & oLedger.Entries.Count.ToString())

        SaveLedger(oLedger)
        If Not String.IsNullOrEmpty(query.sLastSentDoc) Then

            If TypeOf currentProvider Is OpenAICompatibleProvider Then
                DirectCast(currentProvider, OpenAICompatibleProvider).RemoveFromHistory(query.sLastSentDoc)
            End If

            query.sLastSentDoc = ""

        End If

        sPromptToProcess = "The Ledger entries were received and stored successfully. Continue the current research autonomously according to the briefing. Determine what relevant information is still missing and continue the research as necessary. Do not ask the user for permission to perform research steps that you can carry out yourself. In Statistic research, request the Ledger only when all reasonable research for the current task or subtask has been completed."

        Return "Ledger entries received and stored."

    End Function

    Private Function ValidateLedgerEntriesJson(ByVal jResponse As JsonElement, ByRef sError As String) As Boolean

        sError = ""

        Dim jValue As JsonElement

        If Not jResponse.TryGetProperty("ledger_name", jValue) OrElse jValue.ValueKind <> JsonValueKind.String OrElse String.IsNullOrWhiteSpace(jValue.GetString()) Then
            sError = "Required field 'ledger_name' is missing or empty."
            Return False
        End If

        If Not jResponse.TryGetProperty("purpose", jValue) OrElse jValue.ValueKind <> JsonValueKind.String OrElse String.IsNullOrWhiteSpace(jValue.GetString()) Then
            sError = "Required field 'purpose' is missing or empty."
            Return False
        End If

        If Not jResponse.TryGetProperty("result_unit", jValue) OrElse jValue.ValueKind <> JsonValueKind.String OrElse String.IsNullOrWhiteSpace(jValue.GetString()) Then
            sError = "Required field 'result_unit' is missing or empty."
            Return False
        End If

        Dim jEntries As JsonElement
        If Not jResponse.TryGetProperty("entries", jEntries) OrElse jEntries.ValueKind <> JsonValueKind.Array Then
            sError = "Required field 'entries' is missing or is not an array."
            Return False
        End If

        If jEntries.GetArrayLength() = 0 Then
            sError = "The 'entries' array is empty."
            Return False
        End If

        Dim iEntry As Integer = 0

        For Each jEntry As JsonElement In jEntries.EnumerateArray()
            iEntry += 1

            If jEntry.ValueKind <> JsonValueKind.Object Then
                sError = "Ledger entry " & iEntry.ToString() & " is not a JSON object."
                Return False
            End If

            If Not jEntry.TryGetProperty("file_path", jValue) OrElse jValue.ValueKind <> JsonValueKind.String OrElse String.IsNullOrWhiteSpace(jValue.GetString()) Then
                sError = "Ledger entry " & iEntry.ToString() & " is missing required field 'file_path' or the field is empty."
                Return False
            End If

            If Not jEntry.TryGetProperty("file_date", jValue) OrElse jValue.ValueKind <> JsonValueKind.String OrElse String.IsNullOrWhiteSpace(jValue.GetString()) Then
                sError = "Ledger entry " & iEntry.ToString() & " is missing required field 'file_date' or the field is empty."
                Return False
            End If

            If Not jEntry.TryGetProperty("facts", jValue) OrElse jValue.ValueKind <> JsonValueKind.String OrElse String.IsNullOrWhiteSpace(jValue.GetString()) Then
                sError = "Ledger entry " & iEntry.ToString() & " is missing required field 'facts' or the field is empty."
                Return False
            End If
        Next

        Return True

    End Function

    Private Sub SendLedgerEntriesCorrectionPrompt(ByVal sSchemaError As String)

        Dim sb As New StringBuilder

        sb.AppendLine("Your LedgerEntries message was rejected because it did not match the mandatory LedgerEntries format.")
        sb.AppendLine("Error: " & sSchemaError)
        sb.AppendLine()
        sb.AppendLine("Nothing from the rejected message was stored.")
        sb.AppendLine("Send the COMPLETE LedgerEntries message again now, including ALL entries from the rejected message.")
        sb.AppendLine()
        sb.AppendLine("Use EXACTLY this structure and EXACTLY these field names:")
        sb.AppendLine()
        sb.AppendLine("{")
        sb.AppendLine("  ""action"": ""LedgerEntries"",")
        sb.AppendLine("  ""ledger_name"": ""<stable name of this Ledger>"",")
        sb.AppendLine("  ""purpose"": ""<purpose of this Ledger>"",")
        sb.AppendLine("  ""result_unit"": ""<what one final result unit represents>"",")
        sb.AppendLine("  ""entries"": [")
        sb.AppendLine("    {")
        sb.AppendLine("      ""file_path"": ""<complete original pathname>"",")
        sb.AppendLine("      ""file_date"": ""<file date>"",")
        sb.AppendLine("      ""facts"": ""<compact task-relevant information>""")
        sb.AppendLine("    }")
        sb.AppendLine("  ]")
        sb.AppendLine("}")
        sb.AppendLine()
        sb.AppendLine("MANDATORY:")
        sb.AppendLine("- Include ledger_name, purpose, result_unit and entries.")
        sb.AppendLine("- Every entry must contain file_path, file_date and facts.")
        sb.AppendLine("- Use the exact field names shown above. Do not use alternatives such as path instead of file_path or date instead of file_date.")
        sb.AppendLine("- Resend ALL entries from the rejected message, not only corrected or additional entries.")
        sb.AppendLine("- Return only the corrected JSON object.")

        LogDebug("------- Invalid LedgerEntries format. Correction prompt sent to agent.")
        sPromptToProcess = sb.ToString()

    End Sub

    Private Function SaveLedger(ByVal oLedger As TAgentLedger) As String

        Dim sb As New StringBuilder

        sb.AppendLine("--------------------- LEDGER ---------------------")
        sb.AppendLine("Ledger name: " & oLedger.Name)
        sb.AppendLine("Purpose: " & oLedger.Purpose)
        sb.AppendLine("Result unit: " & oLedger.ResultUnit)
        sb.AppendLine("Files in Ledger: " & oLedger.Entries.Count.ToString())
        sb.AppendLine()

        Dim iNr As Integer = 0

        For Each oEntry As TAgentLedgerEntry In oLedger.Entries.Values
            iNr += 1
            sb.AppendLine("[" & iNr.ToString() & "]")
            sb.AppendLine("File: " & oEntry.FilePath)

            If oEntry.FileDate.HasValue Then sb.AppendLine("Date: " & oEntry.FileDate.Value.ToString("dd.MM.yyyy HH:mm:ss"))

            sb.AppendLine("Facts: " & oEntry.Facts)
            sb.AppendLine()
        Next

        sb.AppendLine("--------------------- END OF LEDGER ---------------------")
        sb.AppendLine("Total files in Ledger: " & oLedger.Entries.Count.ToString())

        Dim sSafeLedgerName As String = oLedger.Name
        For Each c As Char In Path.GetInvalidFileNameChars()
            sSafeLedgerName = sSafeLedgerName.Replace(c, "_"c)
        Next

        Dim sLedgerFile As String = Path.Combine(SessionWorkspace, sSafeLedgerName & "_ledger.txt")
        File.WriteAllText(sLedgerFile, sb.ToString(), Encoding.UTF8)

        LogDebug("Ledger file:        " & sLedgerFile)

        Return sb.ToString()

    End Function

    Private Function ProcessRequestLedger(ByVal jResponse As JsonElement) As String

        Dim jLedgerName As JsonElement
        If Not jResponse.TryGetProperty("ledger_name", jLedgerName) OrElse jLedgerName.ValueKind <> JsonValueKind.String Then Return "Ledger request rejected: ledger_name is missing."

        Dim sLedgerName As String = If(jLedgerName.GetString(), "").Trim()
        If sLedgerName.Length = 0 Then Return "Ledger request rejected: ledger_name is empty."

        Dim oLedger As TAgentLedger = Nothing
        If Not query.Ledgers.TryGetValue(sLedgerName, oLedger) Then Return "Requested Ledger '" & sLedgerName & "' does not exist."

        LogDebug("--------------> LEDGER REQUEST")
        LogDebug("Ledger:             " & sLedgerName)
        LogDebug("Files returned:     " & oLedger.Entries.Count.ToString())

        Dim sb As New StringBuilder(SaveLedger(oLedger))

        sb.Insert(0, "The following Ledger was created from relevant information you extracted during the preceding research and has now been returned because you requested it for evaluation." & vbCrLf & vbCrLf)
        sb.AppendLine()
        sb.AppendLine("Evaluate this Ledger now for the purpose for which it was created.")
        sb.AppendLine("Use the complete Ledger as one coherent dataset. Determine which entries are relevant to the requested result, resolve duplicates or conflicting information where possible, and distinguish sufficiently supported entries from relevant entries that still lack required information.")
        sb.AppendLine("The Ledger is persistent working memory and does not by itself mean that the research is complete.")
        sb.AppendLine("If the preceding Context Excerpts revealed individual files that are clearly likely to contain additional useful, more complete, or better contextualized information, now is the appropriate time to request those specific files as full text and evaluate them.")
        sb.AppendLine("Request only files whose full content can materially improve, verify, clarify, or complete the result. Add any relevant new information obtained from them to the Ledger.")
        sb.AppendLine("Derive the result that the available evidence can support.")
        sb.AppendLine("If evaluating the Ledger and any additionally useful full documents completes the user's current research request, proceed toward the final verified answer.")
        sb.AppendLine("If this Ledger belongs only to one part of a larger or multi-part research task, treat this part as completed once its result has been established, preserve that result, and then continue with the remaining part of the user's request.")
        sb.AppendLine("Do not repeat earlier broad retrieval merely because the original Excerpts are no longer present. The Ledger contains the task-relevant information preserved from those earlier analyses. Targeted requests for specific full documents identified through the Excerpts or Ledger are explicitly allowed and are not considered repeated retrieval.")
        sPromptToProcess = sb.ToString()
        If currentSession.AgentResearchMode = "statistic" Then
            Return "Research in Excerpts " & oLedger.Name & " is complete. The agent is now evaluating the collected results. "
        Else
            Return "Research in Excerpts " & oLedger.Name & " is complete. The agent is now evaluating the findings and may continue with targeted research."
        End If

    End Function

    Private Function ProcessAction_SearchStrategy(jResponse As JsonElement, ByVal sSearchType As String) As Boolean

        Dim jStrategy As JsonElement

        If Not jResponse.TryGetProperty("strategy", jStrategy) OrElse
       jStrategy.ValueKind <> JsonValueKind.Object Then

            sPromptToProcess =
            "Your search request is missing the mandatory strategy object. " &
            "Please send the complete FullSearch or HitlistSearch request again, including strategy and search parameters in the same JSON message. " &
            "Do not inform the user about this internal correction."

            Return False
        End If

        Dim sTaskType As String = GetJsonString(jStrategy, "task_type")
        Dim sDocumentPopulation As String = GetJsonString(jStrategy, "document_population")
        Dim sApplicableRules As String = GetJsonString(jStrategy, "applicable_rules")
        Dim sRuleImplementation As String = GetJsonString(jStrategy, "rule_implementation")
        Dim sResearchMode As String = GetJsonString(jStrategy, "research_mode")
        Dim sStatExptSize As String = GetJsonString(jStrategy, "stat_expt_size")
        Dim sExtractionTarget As String = GetJsonString(jStrategy, "extraction_target")

        If String.IsNullOrWhiteSpace(sTaskType) OrElse
       String.IsNullOrWhiteSpace(sDocumentPopulation) OrElse
       String.IsNullOrWhiteSpace(sApplicableRules) OrElse
       String.IsNullOrWhiteSpace(sRuleImplementation) OrElse
       String.IsNullOrWhiteSpace(sResearchMode) OrElse
       String.IsNullOrWhiteSpace(sExtractionTarget) Then

            sPromptToProcess =
            "The strategy object in your search request is incomplete. " &
            "Please send the complete FullSearch or HitlistSearch request again with all required strategy fields and search parameters in the same JSON message. " &
            "Do not inform the user about this internal correction."

            Return False
        End If

        If Not sResearchMode.Equals("Knowledge", StringComparison.OrdinalIgnoreCase) AndAlso
       Not sResearchMode.Equals("Statistic", StringComparison.OrdinalIgnoreCase) Then

            sPromptToProcess =
            "The strategy field research_mode must be exactly Knowledge or Statistic. " &
            "Please send the complete search request again with a valid strategy object. " &
            "Do not inform the user about this internal correction."

            Return False
        End If

        LogDebug("--------------> SEARCH STRATEGY")
        LogDebug("Search type:          " & sSearchType)
        LogDebug("Task type:            " & sTaskType)
        LogDebug("Document population:  " & sDocumentPopulation)
        LogDebug("Applicable rules:     " & sApplicableRules)
        LogDebug("Rule implementation:  " & sRuleImplementation)
        LogDebug("Research mode:        " & sResearchMode)
        LogDebug("StatExptSize:         " & If(String.IsNullOrWhiteSpace(sStatExptSize), "<none>", sStatExptSize))
        LogDebug("Extraction target:    " & sExtractionTarget)
        LogDebug("-----------------------------------------")

        Return True

    End Function

#End Region
    Private Function ProcessAction_ResearchMode(ByVal jResponse As JsonElement) As String

        Try
            Dim modeElement As JsonElement

            If Not jResponse.TryGetProperty("mode", modeElement) OrElse modeElement.ValueKind <> JsonValueKind.String Then
                sPromptToProcess = "Your researchmode response is missing the required 'mode' field. Reply again with mode 'Knowledge' or 'Statistic'."
                Return ""
            End If
            Dim sBriefingFile As String
            currentSession.AgentResearchMode = modeElement.GetString().Trim().ToLowerInvariant
            Select Case currentSession.AgentResearchMode
                Case "knowledge"
                    sBriefingFile = "Agent_01_KnowledgeBriefing.md"
                Case "statistic"
                    sBriefingFile = "Agent_01_StatisticBriefing.md"
                Case Else
                    currentSession.AgentResearchMode = ""
                    sPromptToProcess = "Invalid research mode. Reply again with action 'researchmode' and mode 'Knowledge' or 'Statistic'."
                    Return ""
            End Select
            LogDebug("--------------> RESEARCH MODE: " & currentSession.AgentResearchMode)
            LogDebug("--------------> MAIN BRIEFING: " & sBriefingFile)
            sPromptToProcess = BuildMainAgentPrompt(sBriefingFile)
            If String.IsNullOrWhiteSpace(sPromptToProcess) Then
                LogDebug("!!!!!!!!!! Main Agent prompt could not be created with briefing: " & sBriefingFile)
                Return "Problem preparing Agent briefing"
            End If

            Return ""

        Catch ex As Exception
            LogDebug("Problem processing research mode: " & ex.Message)
            Return "Problem processing research mode"
        End Try

    End Function

    Private Function GetJsonString(jResponse As JsonElement, sProperty As String) As String
        Dim element As JsonElement
        If Not jResponse.TryGetProperty(sProperty, element) Then Return ""
        If element.ValueKind = JsonValueKind.Null Then Return ""
        Return element.ToString().Trim()
    End Function

#Region "Findit6 search requests"

    ' Processes a FullSearch or HitlistSearch requested by the agent.
    ' Invalid search requests are rejected and returned to the agent for correction.
    Private Function ProcessAction_Search(jResponse As JsonElement, action As String) As String
        Try
            If action = "hitlistsearch" Then
                Dim hitlistName As String = jResponse.GetProperty("hitlist").GetString()

                If String.IsNullOrWhiteSpace(hitlistName) OrElse Path.GetFileName(hitlistName) <> hitlistName OrElse Not String.Equals(Path.GetExtension(hitlistName), ".fil", StringComparison.OrdinalIgnoreCase) Then
                    LogDebug("[WARN] Agent sent invalid hitlist filename: " & hitlistName)
                    sPromptToProcess = "Your hitlistsearch request was invalid. The 'hitlist' field must contain only the filename of the existing .fil result file that you want Findit6 to search within, for example 'Agent_FullSearch_20260817_143433.fil'. Do not send a document filename or a full path. Please repeat the search request with the correct .fil filename."
                    Return "Agent sent an invalid Findit6 search, was warned, will try again."
                End If

                Dim hitlistPath As String = Path.Combine(SessionWorkspace, hitlistName)
                If Not File.Exists(hitlistPath) Then
                    LogDebug("[WARN] Agent requested missing hitlist: " & hitlistName)
                    sPromptToProcess = "Your hitlistsearch request references a .fil file that does not exist in the current search session: '" & hitlistName & "'. Use the filename of an existing .fil result previously returned by Findit6 and repeat the search request."
                    Return "Agent sent an invalid Findit6 search, was warned, will try again."
                End If
            End If

            Dim newFijName As String = CreateAgentSearchFij(jResponse, action)
            Dim newFijPath As String = Path.Combine(SessionWorkspace, newFijName)
            ApplyAgentSearchChanges(newFijPath, jResponse)

            sFinditResultfile = Path.ChangeExtension(newFijName, ".fil")
            Return startAgentsFij(newFijName, action)

        Catch ex As Exception
            LogDebug("Problem processing incoming fij: " & ex.Message)
            Return "Problem processing incoming Findit6 search."
        End Try
    End Function

    ' Creates the FIJ file for the next agent-controlled Findit6 search.
    ' FullSearch starts from Standard.fij; HitlistSearch copies the selected FIL and switches it to Where.Mode=2.
    ' Returns the new FIJ filename inside SessionWorkspace.
    Private Function CreateAgentSearchFij(jResponse As JsonElement, action As String) As String
        Dim timeStamp As String = DateTime.Now.ToString("yyyyMMdd_HHmmss")

        If action = "fullsearch" Then
            Dim newFijName As String = "Agent_FullSearch_" & timeStamp & ".fij"
            Dim sourceFij As String = Path.Combine(AgentWorkspace, "Standard.fij")
            Dim targetFij As String = Path.Combine(SessionWorkspace, newFijName)
            File.Copy(sourceFij, targetFij, True)
            Return newFijName
        End If

        Dim sourceFilName As String = jResponse.GetProperty("hitlist").GetString()

        If String.IsNullOrWhiteSpace(sourceFilName) OrElse Path.GetFileName(sourceFilName) <> sourceFilName OrElse Not String.Equals(Path.GetExtension(sourceFilName), ".fil", StringComparison.OrdinalIgnoreCase) Then
            Throw New ArgumentException("Invalid hitlist file: " & sourceFilName)
        End If

        Dim sourceFil As String = Path.Combine(SessionWorkspace, sourceFilName)
        If Not File.Exists(sourceFil) Then Throw New FileNotFoundException("Hitlist not found.", sourceFil)

        Dim hitlistFijName As String = "Agent_HitlistSearch_" & timeStamp & ".fij"
        Dim hitlistFijPath As String = Path.Combine(SessionWorkspace, hitlistFijName)
        File.Copy(sourceFil, hitlistFijPath, True)

        Dim ini As New IniFile(hitlistFijPath)
        ini.WriteValue("Where", "Mode", "2")
        ini.WriteValue("Where", "Name", sourceFilName)

        Dim oldHistory As String = ini.ReadValue("Where", "History", "")
        If String.IsNullOrWhiteSpace(oldHistory) Then
            ini.WriteValue("Where", "History", sourceFilName)
        Else
            ini.WriteValue("Where", "History", oldHistory & " -> " & sourceFilName)
        End If

        Return hitlistFijName
    End Function

    ' Applies the agent-provided changes array to a prepared FIJ file.
    ' Values are converted to INI-compatible strings and Fulltext/SearchString is mirrored to currentSession.SearchTerm.
    Private Sub ApplyAgentSearchChanges(fijPath As String, jResponse As JsonElement)
        currentSession.sExcerptsSize = ""
        Dim changesElement As JsonElement
        If Not jResponse.TryGetProperty("changes", changesElement) Then Exit Sub
        If changesElement.ValueKind <> JsonValueKind.Array Then Exit Sub
        Dim fijIni As New IniFile(fijPath)
        For Each change As JsonElement In changesElement.EnumerateArray()
            Dim section As String = change.GetProperty("section").GetString()
            Dim key As String = change.GetProperty("key").GetString()
            Dim value As String = JsonValueToIniString(change.GetProperty("value"), section, key)
            fijIni.WriteValue(section, key, value)
            If section.Equals("Fulltext", StringComparison.OrdinalIgnoreCase) AndAlso key.Equals("SearchString", StringComparison.OrdinalIgnoreCase) Then
                currentSession.SearchTerm = value
            End If
            If key.Equals("StatExptSize", StringComparison.OrdinalIgnoreCase) Then
                currentSession.sExcerptsSize = value
            End If
        Next
    End Sub

    ' Converts primitive JSON values into the string representation expected by the INI writer.
    ' Accepts strings, booleans and numbers while preserving numeric text exactly.
    Private Function JsonValueToIniString(valueElement As JsonElement, section As String, key As String) As String
        Select Case valueElement.ValueKind
            Case JsonValueKind.String
                Return valueElement.GetString()
            Case JsonValueKind.True
                Return "True"
            Case JsonValueKind.False
                Return "False"
            Case JsonValueKind.Number
                Return valueElement.GetRawText()
            Case Else
                Throw New InvalidDataException("Unsupported JSON value type for " & section & "/" & key & ": " & valueElement.ValueKind.ToString())
        End Select
    End Function
#End Region

#Region "Timeout handling"

    ' Starts timeout recovery when exactly one provider prompt has remained unanswered for too long.
    ' Prepares a reminder only once, leaves PromptBalance untouched and marks the reminder as pending until sent.
    Private Sub CheckAgentTimeoutReminder()

        If currentSession.blocal Then Exit Sub

        ' A timeout recovery is already running.
        If bTimeoutRecovery Then Exit Sub

        ' After a successful timeout recovery, give the previously outstanding
        ' response additional time to arrive. Do not send another reminder meanwhile.
        If frmMain.bRecoveryGracePeriod Then

            Dim secondsSinceRecovery As Integer = CInt(DateTime.Now.Subtract(frmMain.dtRecoveryResponse).TotalSeconds)

            If secondsSinceRecovery <= RecoveryGraceSeconds Then Exit Sub

            ' One response from before/during recovery is still considered outstanding.
            ' Assume exactly this one response was lost. Do NOT reset the complete balance,
            ' because newer prompts may meanwhile also be outstanding.
            Dim balance As Integer

            If frmMain.TryDecrementPromptBalance(balance) Then
                LogDebug("Recovery grace period expired. Assuming one outstanding response was lost. PromptBalance = " & balance.ToString())
            Else
                LogDebug("Recovery grace period expired, but PromptBalance is already 0")
            End If

            frmMain.bRecoveryGracePeriod = False
            frmMain.dtRecoveryResponse = DateTime.MinValue

            Exit Sub
        End If

        ' A reminder only makes sense if exactly one prompt is waiting for a response.
        If Threading.Volatile.Read(iPromptBalance) <> 1 Then Exit Sub

        ' Do not create another prompt while one is already waiting to be sent.
        If Not String.IsNullOrWhiteSpace(sPromptToProcess) Then Exit Sub

        Dim timeoutSeconds As Integer = GetAgentTimeoutSeconds()
        Dim secondsSincePrompt As Integer = CInt(DateTime.Now.Subtract(dtLastPromptSent).TotalSeconds)

        If secondsSincePrompt <= timeoutSeconds Then Exit Sub

        Try

            Dim warningTemplate As String = File.ReadAllText(Path.Combine(Application.StartupPath, "noanswerWarning.txt"))

            sPromptToProcess = "We have not received an answer from you for " & timeoutSeconds.ToString() & " seconds." & vbCrLf & vbCrLf & warningTemplate

            ' From now on we are in timeout recovery mode.
            ' Do NOT change PromptBalance here.
            bTimeoutRecovery = True
            bReminderPending = True
            sTypeOfLastPrompt = "requestState"

            LogDebug("Timeout recovery started after " & secondsSincePrompt.ToString() & " seconds waiting for a response")

        Catch ex As Exception

            MsgBoxNeu("Problem with NoanswerWarning.txt", MsgBoxStyle.OkOnly)
            LogDebug("Wanted to send timeout warning but could not read noanswerWarning.txt: " & ex.Message)

        End Try

    End Sub

    ' Returns the expected response timeout for the most recently sent prompt type.
    ' Initial prompts get more time, normal chat less, and all other agent traffic uses the default.
    Private Function GetAgentTimeoutSeconds() As Integer
        Select Case sTypeOfLastPrompt
            Case "Starting"
                Return 90
            Case "text"
                Return 25
            Case Else
                Return 45
        End Select
    End Function
#End Region

#Region "Findit6 monitoring"
    Private sFinishedSeenFor As String = ""

    ' Monitors the FIL file belonging to the currently running Findit6 search.
    ' Routes completed/error searches to final processing and running searches to progress or hit-limit handling.
    Private Function MonitorAgentFindit() As String
        If Not bFinditRunning Then Return ""

        If bworkerbusy Then
            LogDebug("MONITOR: skipped because bworkerbusy=True")
            Return ""
        End If

        Dim filPath As String = Path.Combine(SessionWorkspace, sFinditResultfile)
        If Not File.Exists(filPath) Then Return ""

        If Not IsFileReady(filPath) Then
            LogDebug("--------------> Result fil is still locked, waiting for next check")
            Return ""
        End If

        Dim ini As New IniFile(filPath)
        Dim sStatus As String = ini.ReadValue("Status", "Status", "").Trim()
        Select Case sStatus
            Case "Finished"
                If Not String.Equals(sFinishedSeenFor, sFinditResultfile, StringComparison.OrdinalIgnoreCase) Then
                    sFinishedSeenFor = sFinditResultfile
                    LogDebug("MONITOR: Finished detected, waiting one cycle before processing result")
                    Return ""
                End If
                LogDebug("MONITOR: status=Finished -> ProcessFinishedFinditSearch")
                Return ProcessFinishedFinditSearch(filPath, ini, sStatus)

            Case "Error"
                Return ProcessFinishedFinditSearch(filPath, ini, sStatus)

            Case "Finishing", "Running"
                Return ProcessRunningFinditSearch(ini, sStatus)

            Case ""
                Return ""

            Case Else
                LogDebug("Unexpected Findit status: " & sStatus)
                Return ""
        End Select
    End Function

    ' Finalizes a completed Findit6 search after the result file has been released.
    ' Sends errors or the hitlist back to the agent and returns a compact statistics/link message for the UI.
    Private Function ProcessFinishedFinditSearch(filPath As String, ini As IniFile, sStatus As String) As String
        ' Status may already be Finished although Findit6 has not yet completely
        ' released the file. Check once more before processing the final result.
        If Not IsFileReady(filPath) Then
            LogDebug("--------------> Result fil reports " & sStatus & " but is still locked")
            Return ""
        End If

        LogDebug("FINISHED: starting final result processing for " & Path.GetFileName(filPath))

        bFinditRunning = False

        If sStatus = "Error" Then
            sPromptToProcess = "Findit reported an ERROR with this message:" & vbCrLf & ini.ReadValue("Status", "Errortext", "") & vbCrLf & "Please try again using correct Findit6 Syntax"
            LogDebug("FINISHED: error prompt prepared, length=" & sPromptToProcess.Length.ToString())
            ' Important:
            ' This only returns from ProcessFinishedFinditSearch().
            ' agentworkflow() continues and SendPendingAgentPrompt() can send the
            ' prepared error prompt to the AI.
            Return ""
        End If

        Dim sStatExcerptPath As String = Path.ChangeExtension(filPath, ".statexcpt")

        If File.Exists(sStatExcerptPath) Then

            Dim lStatExcerptSize As Long = New FileInfo(sStatExcerptPath).Length
            Dim lMaxStatExcerptSize As Long = CLng(iMaxStatExcerptKB) * 1024L
            If lStatExcerptSize <= lMaxStatExcerptSize Then
                LogDebug("--------------> Sending StateExcerpt (" & (lStatExcerptSize \ 1024L).ToString() & " KB)")
                iWaitingForLedger = 1
                sPromptToProcess = BuildStateExcerptPrompt(sStatExcerptPath) & vbCrLf &
                     "Do not send an interim answer to the user in response to this search result. Do not ask for any advice or Release or Permission. Continue immediately with the next required research action, or send the final synthesis if the research is complete."

            Else
                LogDebug("--------------> StateExcerpt too large (" & (lStatExcerptSize \ 1024L).ToString() & " KB)")

                sPromptToProcess = "The requested Context Excerpt result was created successfully, but became too large to send reliably (" & (lStatExcerptSize \ 1024L).ToString() & " KB; configured limit: " & iMaxStatExcerptKB.ToString() & " KB)." & vbCrLf &
                       "The search itself completed successfully. Continue working with the corresponding hitlist. You may narrow the population, reduce StatExptSize, choose better search terms for the required excerpt content, or combine these approaches before requesting Context Excerpts again." & vbCrLf & vbCrLf &
                       PromptSearchResult(filPath)
            End If
        Else
            sPromptToProcess = PromptSearchResult(filPath)
        End If

        If String.IsNullOrWhiteSpace(sPromptToProcess) Then
            LogDebug("!!!!!!!!!! SearchResult prompt is empty although result fil is finished")
        Else
            LogDebug("FINISHED: result prompt prepared, length=" & sPromptToProcess.Length.ToString())
        End If

        Dim finditResults As New IniFile(filPath)
        iFinditFoundFiles = CInt(finditResults.ReadValue("Statistik", "HitCount", "0"))

        ' Important:
        ' Even with zero hits, agentworkflow() still continues afterwards and can
        ' send sPromptToProcess to the AI.
        If iFinditFoundFiles = 0 Then Return "Findit did not Find any file containing searched words"

        Dim iFinditSearchedFiles As Long = CLng(finditResults.ReadValue("Statistik", "AllFilesCount", "0"))
        Dim sFinditVolume As String = finditResults.ReadValue("Statistik", "AllFilesSize", "0")
        Dim finditVolumeGB As Double = CDbl(sFinditVolume) / 1024 / 1024 / 1024
        CurrentAction = ChunkActionEnum.Rerank

        Dim linkUrl As String = New Uri(filPath).AbsoluteUri
        Dim displayText As String = Path.GetFileName(filPath)
        Dim sReturn As String = " Findit6 found " & iFinditFoundFiles.ToString() & " results in " & iFinditSearchedFiles.ToString("N0", CultureInfo.CurrentCulture) & " searched files with " & finditVolumeGB.ToString("N1", CultureInfo.CurrentCulture) & " GB in: <|FILELINK|>" & linkUrl & "<|DISPLAY|>" & displayText & "<|ENDLINK|>"
        If currentSession.sExcerptsSize <> "" Then sReturn &= vbCrLf & "Agent is analysing Excerpts"
        Return sReturn
    End Function

    ' Handles a Findit6 search that is still running or finishing.
    ' Requests refinement when the hit threshold is exceeded; otherwise external models receive periodic status updates.
    Private Function ProcessRunningFinditSearch(ini As IniFile, sStatus As String) As String
        Dim iFoundFiles As Integer = CInt(ini.ReadValue("Statistik", "Hitcount", "0"))

        If iFoundFiles > iMaxHits Then
            If Not String.Equals(sTooManyHitsReportedFor, sFinditResultfile, StringComparison.OrdinalIgnoreCase) Then
                sTooManyHitsReportedFor = sFinditResultfile
                LogDebug("--------------> Sending too many hits, try again!")
                sPromptToProcess = PromptToManyHits(iFoundFiles)
            End If

            bFinditRunning = False
            Return "Search gave too many hits, Agent is narrowing the query."
        End If

        If currentSession.blocal Then Return ""

        If DateTime.Now.Subtract(lastStatusSentTime).TotalSeconds >= 30 Then
            lastStatusSentTime = DateTime.Now
            sPromptToProcess = PromptSearchStatus(sFinditResultfile, sStatus)
            LogDebug("--------------> Sending Findit status: " & sStatus)
        End If

        Return ""
    End Function

    Private Function BuildStateExcerptPrompt(ByVal sStatExcerptPath As String) As String
        Dim sb As New StringBuilder
        sb.AppendLine("Here is the context excerpt result of the search you requested:")
        sb.AppendLine()
        sb.AppendLine("The corresponding Findit6 hitlist is stored as: " & Path.GetFileName(Path.ChangeExtension(sStatExcerptPath, ".fil")))
        sb.AppendLine()
        sb.AppendLine(File.ReadAllText(sStatExcerptPath, Encoding.UTF8))
        sb.AppendLine()
        sb.AppendLine("MANDATORY CONTEXT EXCERPT EVALUATION RULES")
        sb.AppendLine()
        sb.AppendLine("Evaluate the complete numbered set of context excerpts systematically before producing a result. Do not silently skip entries merely because they appear repetitive, unimportant or likely to be duplicates.")
        sb.AppendLine()
        sb.AppendLine("If the task involves counting, classification, grouping, comparison or aggregation:")
        sb.AppendLine()
        sb.AppendLine("1. Account for ALL provided excerpts. The excerpt numbering and the final total are authoritative and must be used to verify that the complete set has been considered.")
        sb.AppendLine("2. First determine exactly what kind of entity, event, case, record or occurrence the user actually wants counted or classified. Files, search hits and excerpt blocks are not automatically the requested counting unit.")
        sb.AppendLine("3. Use all available evidence, including excerpt content, filenames, paths, dates, identifiers and other metadata, to distinguish genuinely different entities from duplicates, versions, related records or multiple documents referring to the same entity.")
        sb.AppendLine("4. Do not count separate files as separate entities merely because they are separate documents. Conversely, do not merge similar entries when the available evidence indicates that they represent genuinely different entities or occurrences.")
        sb.AppendLine("5. Resolve as much as possible from the excerpts and metadata first. Request complete documents only for specific cases whose classification cannot be determined reliably from the available information.")
        sb.AppendLine("6. Keep unresolved or genuinely ambiguous cases separate from clearly established cases. Do not silently guess.")
        sb.AppendLine("7. Before giving the final result, explicitly verify that every numbered excerpt has been accounted for and that the reported number refers to exactly the counting unit requested by the user.")
        sb.AppendLine("8. If uncertainty remains, report the clearly established result separately from the ambiguous cases and state an appropriate range or explain how different reasonable classifications affect the result.")
        sb.AppendLine()
        sb.AppendLine("Do not report a final count merely because a plausible number has emerged during the analysis. Complete and verify the full evaluation first.")
        sb.AppendLine()
        sb.AppendLine("Do not send an interim answer to the user in response to this search result. Continue immediately with the next required research action, or send the final synthesis if the research is complete.")

        Return sb.ToString()
    End Function
#End Region

#Region "Prompt and search communication"

    ' Sends sPromptToProcess through the common provider path when a prompt is waiting.
    ' Clears the buffer before sending and restores it if the send call throws.
    Private Sub SendPendingAgentPrompt()

        Dim bSendingPendingUserPrompt As Boolean = False
        Dim sPendingUserText As String = ""
        ' The user did send a prompt to interrupt the agent work.
        If Not String.IsNullOrWhiteSpace(currentSession.sPendingUserPrompt) Then
            If Not String.IsNullOrWhiteSpace(sPromptToProcess) Then
                ' The Agent has already prepared its next prompt.
                ' Replace it with the waiting user message.
                sPendingUserText = currentSession.sPendingUserPrompt
                sPromptToProcess = sPendingUserText
                currentSession.sPendingUserPrompt = ""
                dtPendingPromptZeroSince = DateTime.MinValue
                bSendingPendingUserPrompt = True
            ElseIf iPromptBalance > 0 Then
                ' An LLM request is still running.
                dtPendingPromptZeroSince = DateTime.MinValue
                Exit Sub
            Else
                ' No LLM request and no Agent prompt waiting.
                ' Make sure PromptBalance really remains at zero for two seconds.
                If dtPendingPromptZeroSince = DateTime.MinValue Then
                    dtPendingPromptZeroSince = Date.Now
                    Exit Sub
                End If
                If Date.Now.Subtract(dtPendingPromptZeroSince).TotalSeconds < 2 Then Exit Sub
                sPendingUserText = currentSession.sPendingUserPrompt
                sPromptToProcess = sPendingUserText
                currentSession.sPendingUserPrompt = ""
                dtPendingPromptZeroSince = DateTime.MinValue
                bSendingPendingUserPrompt = True
            End If
        Else
            dtPendingPromptZeroSince = DateTime.MinValue
        End If
        If String.IsNullOrWhiteSpace(sPromptToProcess) Then Exit Sub
        Dim promptToSend As String = sPromptToProcess
        sPromptToProcess = ""
        If bSendingPendingUserPrompt Then
            ' Show only the real user message in the visible protocol.
            frmMain.UsrMsgToRtf(sPendingUserText)
            ' Inform the LLM that the previous Agent action has been interrupted.
            promptToSend =
            "[Findit6Agent note: The user has interrupted the current Agent workflow and wants to chat. " &
            "Any outstanding action previously requested from Findit6 or Findit6Agent will no longer return a result. " &
            "If the user later asks you to continue the research, repeat your last outstanding request if it is still required.]" &
            vbCrLf & vbCrLf &
            sPendingUserText
            query.bAgentRunning = False
        End If
        sPreviousPompt = promptToSend
        Try
            frmMain.SendPromptAsync(promptToSend, isFirstRequest:=False)
            If bSendingPendingUserPrompt Then
                frmMain.btnSend.Enabled = True
                frmMain.txtPrompt.Enabled = True
                frmMain.txtPrompt.Text = ""
            End If
        Catch ex As Exception
            If bSendingPendingUserPrompt Then
                ' Restore only the original user text, not the internal note.
                currentSession.sPendingUserPrompt = sPendingUserText
            Else
                sPromptToProcess = promptToSend
            End If
            LogDebug("Problem sending Agent prompt: " & ex.Message)
        End Try
    End Sub

    Private dtPendingPromptZeroSince As DateTime = DateTime.MinValue



    ' Checks whether a file can currently be opened exclusively for reading.
    ' Used to avoid parsing a FIL file while Findit6 is still writing or holding it.
    Private Function IsFileReady(filePath As String) As Boolean
        Try
            Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None)
            End Using
            Return True
        Catch ex As IOException
            Return False
        End Try
    End Function

    ' Prepares a corrective prompt after the agent returned invalid JSON.
    ' The next workflow cycle sends the correction request so the agent can repeat its response in valid protocol syntax.
    Private Sub HandleJsonParseError()
        sPromptToProcess = "Sorry, it seems your previous message was not transmitted in legal JSON format. Please follow all rules for acceptable JSON formats and field names that were initially submitted and try again."
        LogDebug("--------> Prepared [JSON FIX PROMPT] " & sPromptToProcess)
    End Sub

    ' Starts Findit6 by shell-opening a prepared FIJ file in the current session workspace.
    ' Marks Findit6 as running, initializes status timing and returns the search-start text for the UI.
    Public Function startAgentsFij(fijToStart As String, action As String) As String
        ' The agent has requested a Findit6 search.
        Dim fijPath As String = Path.Combine(SessionWorkspace, fijToStart)
        If Not File.Exists(fijPath) Then
            Throw New FileNotFoundException("FIJ file not found.", fijPath)
        End If
        Dim procInfo As New System.Diagnostics.ProcessStartInfo()
        procInfo.FileName = fijPath
        procInfo.WorkingDirectory = SessionWorkspace
        procInfo.UseShellExecute = True
        System.Diagnostics.Process.Start(procInfo)
        bFinditRunning = True
        ' Delay the first status update slightly so Findit6 has time to start.
        lastStatusSentTime = DateTime.Now.AddSeconds(5)
        Dim sExcerpt As String = ""
        If currentSession.sExcerptsSize <> "" Then
            sExcerpt = " (ExecerptBuilding)"
        End If
        If action = "fullsearch" Then
            Return "Findit6 is processing full search: " & sExcerpt & vbCrLf & currentSession.SearchTerm
        Else
            Return "Findit6 is processing hitlist search: " & sExcerpt & vbCrLf & currentSession.SearchTerm
        End If
    End Function

    ' Builds the prompt that returns a completed Findit6 hitlist to the agent.
    ' Sends the result rows plus the hitlist filename and a short explanation of the relevant metadata fields.
    Private Function PromptSearchResult(sFinditResultfile As String) As String
        Try
            Dim filPath As String = If(Path.IsPathRooted(sFinditResultfile), sFinditResultfile, Path.Combine(SessionWorkspace, sFinditResultfile))
            If Not File.Exists(filPath) Then Return ""

            Dim filContent As String = File.ReadAllText(filPath)
            Dim Resultlist As String = ExtractResultList(filContent)
            Dim sHitlistName As String = Path.GetFileName(filPath)
            Dim iHitCount As Integer = Resultlist.Split(New String() {vbCrLf}, StringSplitOptions.RemoveEmptyEntries).Length
            Dim prompt As String

            If currentSession.AgentResearchMode = "statistic" AndAlso iHitCount > 25 Then
                prompt = "Search completed." & vbCrLf &
         "Hitlist: " & sHitlistName & vbCrLf &
         "Results: " & iHitCount.ToString() & vbCrLf & vbCrLf &
"Use this hitlist as the candidate population for one or more targeted HitlistSearch operations. The next HitlistSearch may either refine the population, create Context Excerpts, or do both at the same time." & vbCrLf &
"Choose the search terms not only to decide which documents remain, but also to control which passages are exposed in the Context Excerpts. Whenever possible, select terms that bring the concrete fields needed for counting, classification, deduplication or aggregation into view." & vbCrLf &
"Context Excerpts may themselves reveal better terms, alternative wording or document structures. If useful, use what you learn from one Excerpt search to perform another HitlistSearch and create a better focused set of Context Excerpts." & vbCrLf &
"At the same time, preserve recall: do not narrow the population with overly restrictive AND conditions unless those terms are very likely to occur in every relevant document. Prefer broader OR groups and alternative terms when different documents may use different wording." & vbCrLf &
"The goal is to iteratively reduce irrelevant material while preserving all plausibly relevant documents and exposing the information required for the statistical analysis."
            Else
                prompt = "Search completed." & vbCrLf &
                     "Hitlist: " & sHitlistName & vbCrLf & vbCrLf &
                     "Each result line contains the file path and Findit6 result metadata. The fourth field contains the search phrases found in the file; the last field contains the file's last modification date." & vbCrLf & vbCrLf &
                     Resultlist & vbCrLf
            End If

            LogDebug("----------->Send SearchResult")
            sTypeOfLastPrompt = "result"
            Return prompt & vbCrLf & "Do not send an interim answer to the user in response to this search result. Do not ask for any advice or Release or Permission. Continue immediately with the next required research action, or send the final synthesis if the research is complete."


        Catch ex As Exception
            MsgBox("Error in PromptSearchResult: " & ex.Message, MsgBoxStyle.OkOnly)
            Return ""
        End Try
    End Function

    ' Builds a refinement request when a running search exceeds the configured hit threshold.
    ' The agent is asked to narrow the search instead of continuing with an excessively broad result set.
    Private Function PromptToManyHits(hitcount As Integer) As String
        Dim refinementPrompt As String = "Search yielded " & hitcount & " results (threshold: " & iMaxHits & ")." & vbCrLf & "Please refine your search strategy for more precision." & vbCrLf & "Create a more specific *.fij with additional constraints." & vbCrLf & "Do not inform the user, do not ask for advice, just start the next plausible step of your work"
        sTypeOfLastPrompt = "PleaseRefine"
        Return refinementPrompt
    End Function

    ' Builds a compact status prompt from the current FIL while Findit6 is still running.
    ' The agent can either acknowledge normal progress or report a critical search error.
    Private Function PromptSearchStatus(sFinditResultfile As String, sStatusSnapshot As String, Optional retryCount As Integer = 0) As String
        Try
            Dim filPath As String = Path.Combine(SessionWorkspace, sFinditResultfile)
            Dim ini As New IniFile(filPath)

            Dim sStatus As String = ""
            sStatus &= "Status=" & sStatusSnapshot & vbCrLf
            sStatus &= "FoundFiles=" & ini.ReadValue("Statistik", "Hitcount", "") & vbCrLf
            sStatus &= "Progress=" & ini.ReadValue("Status", "Progress", "Error") & vbCrLf
            sStatus &= "PdfOCR=" & ini.ReadValue("Status", "PdfOCR", "") & vbCrLf
            sStatus &= "Limitation=" & ini.ReadValue("Status", "Limitation", "") & vbCrLf
            sStatus &= "Errortext=" & ini.ReadValue("Status", "Errortext", "") & vbCrLf
            sStatus &= "Errorquery=" & ini.ReadValue("Status", "Errorquery", "") & vbCrLf & vbCrLf
            sStatus &= "SearchedFilesCount=" & ini.ReadValue("Statistik", "SearchedFilesCount", "0") & vbCrLf
            sStatus &= "Duration=" & ini.ReadValue("Statistik", "Duration", "") & vbCrLf

            Dim prompt As String = "Findit search is running. Please analyze this status:" & vbCrLf & vbCrLf & sStatus & vbCrLf & vbCrLf &
                               "Please respond with EITHER:" & vbCrLf &
                               "1. {""action"":""waiting"",""text"":""status_normal_continue""} if everything looks normal" & vbCrLf &
                               "2. {""action"":""answer"",""text"":""Error detected: [description]""} if you detect critical errors"

            LogDebug("----------->PromptingSearchStatus (Attempt " & (retryCount + 1) & ", Status=" & sStatusSnapshot & ")")
            sTypeOfLastPrompt = "status"
            Return prompt

        Catch ex As InvalidOperationException When ex.Message.Contains("busy")
            LogDebug("Could not create the current Findit6 status prompt")
            Return ""
        End Try
    End Function
#End Region

#Region "Agent Response Actions"

    ' Extracts normal chat text from an action=answer response.
    ' Returns the text for direct display in the user interface.
    Private Function ProcessAction_Text(jResponse As JsonElement) As String
        Try
            Return jResponse.GetProperty("text").GetString()
        Catch ex As Exception
            Return "Error: " & ex.Message
        End Try
    End Function

    ' Resolves an agent-requested source path to its cached UTF-8 export and prepares the file content for sending.
    ' Missing exports produce a corrective agent prompt; successful requests are added to the RTF protocol as links.
    Private Function ProcessAction_RequestFile(jResponse As JsonElement) As String
        ' Send the UTF-8 export of the requested file to the agent.
        LogDebug("----------->ProcessAction_RequestFile")
        Try
            ' Read the requested path exactly as supplied by the agent.
            Dim Orgfilename As String = jResponse.GetProperty("filename").GetString()
            Dim utf8Filename As String = HashPathName(Orgfilename)
            ' Derive the cached UTF-8 export filename from the requested path.
            Dim utf8filePath As String = Path.Combine(sUTFpath, utf8Filename) & ".txt"
            ' Verify that the UTF-8 export exists.
            If Not File.Exists(utf8filePath) Then
                sPromptToProcess = "Sorry, this requested document does not seem to exist as a UTF8 export for once, please try another one, thanks!"
                Return "UTF8Copy not found: " & Orgfilename & " | " & utf8Filename
            End If
            ' Read the UTF-8 export.
            Dim fileContent As String = File.ReadAllText(utf8filePath, System.Text.Encoding.UTF8)
            ' Prepare the document content for the next provider prompt.
            sPromptToProcess = "------ Here is the requested file content:" & vbCrLf & vbCrLf & fileContent & vbCrLf & "------ End of File" & vbCrLf & vbCrLf &
                   "Continue the current research autonomously according to the briefing. Use this document as evidence and perform any further useful research steps without asking the user for permission. Ask the user only if further progress is genuinely blocked and requires information or a decision that only the user can provide."
            query.sLastSentDoc = sPromptToProcess
            ' Insert the filename directly into the RTF protocol so it can be rendered as a link.
            ' No additional UI text is returned through the timer.
            frmMain.AppendMessageWithLink(Orgfilename)
            Return ""
        Catch ex As Exception
            Return "Error reading file: " & ex.Message
        End Try
    End Function

#End Region

#Region "Agent initialisation and Rules"

    Public Function LoadAgentRules(ByVal sBriefingFile As String) As String
        Dim sb As New StringBuilder()

        Try
            Dim briefingFile As String = Path.Combine(AgentWorkspace, sBriefingFile)
            Dim syntaxFile As String = Path.Combine(AgentWorkspace, "Agent_02_Syntax.md")
            Dim communicationFile As String = Path.Combine(AgentWorkspace, "Agent_03_Communication.md")
            Dim rulesFile As String = Path.Combine(AgentWorkspace, "Agent_04_Rules.md")
            Dim userRulesFile As String = Path.Combine(AgentWorkspace, "chunk_Query_UserRules.md")

            If Not File.Exists(briefingFile) Then Throw New FileNotFoundException("Agent briefing file not found.", briefingFile)
            If Not File.Exists(syntaxFile) Then Throw New FileNotFoundException("Agent syntax file not found.", syntaxFile)
            If Not File.Exists(communicationFile) Then Throw New FileNotFoundException("Agent communication file not found.", communicationFile)
            If Not File.Exists(rulesFile) Then Throw New FileNotFoundException("Agent rules file not found.", rulesFile)

            sb.AppendLine(File.ReadAllText(briefingFile))
            sb.AppendLine("")
            sb.AppendLine(File.ReadAllText(syntaxFile))
            sb.AppendLine(File.ReadAllText(communicationFile))
            sb.AppendLine(File.ReadAllText(rulesFile))

            If IO.File.Exists(userRulesFile) Then
                sb.AppendLine("And here some more mandatory User Rules for finding special files in his archive:")
                sb.AppendLine("")
                sb.AppendLine(ReadMarkedSection(userRulesFile, "<!-- User_RULES_BEGIN -->", "<!-- User_RULES_END -->"))
            End If

            Return sb.ToString()

        Catch ex As Exception
            LogDebug("Error loading Agent Mode rules: " & ex.Message)
            MsgBoxNeu("Agent Mode cannot be started because the system rules could not be loaded:" & vbCrLf & vbCrLf & ex.Message, MsgBoxStyle.OkOnly)
            Return ""
        End Try
    End Function


    ' Builds the first Agent Mode prompt for a new research session.
    ' Prepends the complete instruction package, adds the user question and tells the agent to start without unnecessary coordination.
    ' Builds the initial Agent Mode prompt from query rules and optional archive-specific rules.
    ' Archive rules provide user-specific guidance for selecting relevant files.
    Public Function BuildMainAgentPrompt(ByVal sBriefingFile As String) As String

        Dim agentQueryRules As String = LoadAgentRules(sBriefingFile)
        If String.IsNullOrWhiteSpace(agentQueryRules) Then Return ""

        Dim archiveRulesPath As String = Path.Combine(AgentWorkspace, "Agent_User_Rules.md")
        Dim agentArchiveRules As String = If(File.Exists(archiveRulesPath), File.ReadAllText(archiveRulesPath, Encoding.UTF8).Trim(), "")

        If agentArchiveRules = "" Then LogDebug("[INFO] No Agent_User_Rules.md found or file is empty.")

        Dim sb As New StringBuilder()

        sb.AppendLine(agentQueryRules)
        sb.AppendLine()

        If Not String.IsNullOrWhiteSpace(agentArchiveRules) Then
            sb.AppendLine(agentArchiveRules)
            sb.AppendLine()
        End If

        sb.AppendLine("USER QUESTION:")
        sb.AppendLine(query.UserQuestion)
        sb.AppendLine()
        sb.AppendLine("The task may be more complex than it appears at first glance. These Briefings have been written deliberately, and every section is included for a reason. Before every important decision, verify that your planned action is genuinely consistent with the guidance provided here rather than relying only on your first intuition.")
        sb.AppendLine()
        sb.AppendLine("Knowledge Mode Is complete research, Not simple fact lookup." & vbCrLf &
                    "Unless the user explicitly asks For a Short answer, do Not stop once the direct answer has been found. Write a complete report! Before returning the result, review what is discussed in the relevant documents and include important context that helps explain, qualify Or substantiate the answer." & vbCrLf &
                    "Do Not require the user to ask again for relevant details that are already present in the evidence you have collected. Read additional documents when needed to resolve gaps, uncertainty Or important aspects — Not merely to increase the number of sources." & vbCrLf &
                    "Stay focused: depth should come from relevant evidence, Not From unrelated side topics Or unnecessary verbosity.")
        sb.AppendLine()
        sb.AppendLine("You do NOT have to coordinate your search strategy with the user. Only ask if the user's instructions or question contain ambiguities that must be clarified." & vbCrLf & "Else: Just start working.")
        Dim sPrompt As String = sb.ToString()

        sPrompt = sPrompt.Replace("%Username%", currentSession.UserName)
        sPrompt = sPrompt.Replace("%Userinfo%", currentSession.UserContext)
        Return sPrompt

    End Function

    ' Extracts a marked text section from a file.
    ' Throws a clear error when either marker is missing and returns the trimmed content between both markers.
    Private Function ReadMarkedSection(filePath As String, startMarker As String, endMarker As String) As String
        Dim content As String = File.ReadAllText(filePath)
        Dim startPos As Integer = content.IndexOf(startMarker, StringComparison.Ordinal)
        If startPos < 0 Then Throw New InvalidDataException("Start marker not found: " & startMarker)
        startPos += startMarker.Length
        Dim endPos As Integer = content.IndexOf(endMarker, startPos, StringComparison.Ordinal)
        If endPos < 0 Then Throw New InvalidDataException("End marker not found: " & endMarker)
        Return content.Substring(startPos, endPos - startPos).Trim()
    End Function

    Public Sub SaveAgentProtocol()
        With frmMain
            If String.IsNullOrWhiteSpace(SessionWorkspace) Then Exit Sub
            If .rtfbProtokoll.TextLength = 0 Then Exit Sub
            Try
                ' Store a plain black RTF copy of the completed protocol.
                .rtfKopie.Rtf = .rtfbProtokoll.Rtf
                .rtfKopie.SelectAll()
                .rtfKopie.SelectionColor = Color.Black
                .rtfKopie.SaveFile(IO.Path.Combine(SessionWorkspace, "Protokoll.rtf"))
                .rtfKopie.Rtf = ""
                LogDebug("Agent protocol saved.")
            Catch ex As Exception
                LogDebug("Could not save Agent protocol: " & ex.Message)
            End Try
        End With

    End Sub

#End Region

End Module
