
Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Xml


' Contains the complete Guided/Chunk Mode workflow.
' Chunk Mode is designed primarily for smaller local AI models.
' Recommended minimum model size: about 14B parameters.
' The current workflow was tuned primarily with Qwen3 14B.
' Most local-model operations are intentionally sent without chat history.
' A separately installed reranker service is used to prioritize search results.

Module ModeChunk
    Private Class RerankRequest
        Public Property query As String
        Public Property documents As List(Of String)
    End Class

    Private Class RerankResponse
        Public Property model As String
        Public Property results As List(Of RerankResult)
    End Class

    Private Class RerankResult
        Public Property index As Integer
        Public Property score As Double
    End Class

    Private m_RerankerProcess As Process = Nothing
    Private m_CurrentSynthesisQuestion As String = ""


#Region "Chunk Mode Control and Query Workflow"

    ' Stops an active Chunk Mode run and restores the idle UI state.
    ' The cancellation is recorded in the protocol before returning to WaitForNext.
    Public Sub ChunkRun_Aborted()
        frmMain.btnSend.Text = "NEW Question"
        frmMain.btnSend.Enabled = True
        frmMain.btnSend.ForeColor = Color.White
        frmMain.myTimer.Enabled = False
        frmMain.Spinner.Visible = False
        ' Append the cancellation message to the protocol.
        Dim sMsgToUser As String
        If String.IsNullOrWhiteSpace(query.StopReason) Then
            sMsgToUser = "[" & DateTime.Now.Subtract(frmMain.startTime).ToString("mm\:ss") & "] Query cancelled by the user"
        Else
            sMsgToUser = "[" & DateTime.Now.Subtract(frmMain.startTime).ToString("mm\:ss") & "] " & query.StopReason
        End If
        With frmMain.rtfbProtokoll
            .SelectionStart = .TextLength
            .SelectionLength = 0
            .SelectionColor = clr.Text
            .AppendText(sMsgToUser & vbCrLf & vbCrLf)
            .Update()
            .Select(.Text.Length, 0)
            .ScrollToCaret()
            .Update()
        End With
        query.StopReason = ""
        CurrentAction = ChunkActionEnum.FinishedALL
    End Sub

    ' Finalizes a completed Chunk Mode run and restores the idle UI state.
    ' A plain black RTF copy of the protocol is stored in the session workspace.
    Public Sub ChunkRun_Finished()
        With frmMain
            .btnSend.Text = "NEW Question"
            .btnSend.Enabled = True
            .btnSend.ForeColor = Color.White
            .myTimer.Enabled = False
            .Spinner.Visible = False
            ' Store a plain black RTF copy of the completed protocol.
            .rtfKopie.Rtf = .rtfbProtokoll.Rtf
            .rtfKopie.SelectAll()
            .rtfKopie.SelectionColor = Color.Black
            .rtfKopie.SaveFile(IO.Path.Combine(SessionWorkspace, "Protokoll.rtf"))
            .rtfKopie.Rtf = ""
        End With
    End Sub

    ' Runs one timer-driven state-machine cycle for Chunk Mode.
    ' Pending AI responses, Findit6, reranking, excerpting and synthesis are advanced here.
    Public Function ChunkModeRunner() As String
        Dim SMsgToUser As String = ""
        ' Handle a user-requested cancellation.
        If query.bStopQuerry Then
            ChunkRun_Aborted()
            Return ""
        End If
        ' Handle a completely finished run.
        If CurrentAction = ChunkActionEnum.FinishedALL Then
            ChunkRun_Finished()
            Return ""
        End If

        ' Process any pending AI response.
        If KIresponse <> "" Then
            Try
                LogDebug("<<<< RAW KI RESPONSE:")
                Using doc As JsonDocument = JsonDocument.Parse(KIresponse)
                    Dim jResponse As JsonElement = doc.RootElement
                    Dim action As String = jResponse.GetProperty("Action").GetString()
                    LogDebug("<<<< Received action: " & action)
                    If action.Contains("chunk_Query_") Then SMsgToUser = QueryActions(action, jResponse)
                    If action.Contains("detailQuestions") Then
                        SMsgToUser = QuestionsReturned(jResponse)
                        StartRerankWorker()
                    End If
                    If action.ToLower().Contains("excerpt") Then ExcerptReturned(jResponse)
                    If action.ToLower().Contains("synthesisready") Then SMsgToUser = ShowSynthesis(jResponse)
                End Using
            Catch ex As Exception
                If bInvalidJson AndAlso CurrentAction = ChunkActionEnum.ExcerptsRUN Then
                    LogDebug("[WARN] Invalid JSON during excerpt processing - file skipped: " & query.ExcerptFile & " | " & ex.Message)
                    bInvalidJson = False
                    RunNextExcerptFromRerankResults()
                Else
                    SMsgToUser = KIresponse
                End If
            End Try
            KIresponse = ""
        End If

        ' Build the rerank file list after Findit6 has completed.
        ' The result file is prepared before starting Findit6 so the previous run stays available.
        If CurrentAction = ChunkActionEnum.Rerank Then
            If m_RerankerStartupState = RerankerStartupState.Ready Then
                BuildRerankFileList(sFinditResultfile)
            ElseIf m_RerankerStartupState = RerankerStartupState.Failed Then
                LogDebug("[ERROR] Reranker unavailable: " & m_RerankerStartupError)
                SMsgToUser = "The reranker could not be started. The query has been cancelled."
                CurrentAction = ChunkActionEnum.FinishedALL
            End If
        End If
        ' Poll the Findit6 result file while the search is running.
        If CurrentAction = ChunkActionEnum.FinditRUNNING Then
            If IO.File.Exists(sFinditResultfile) Then
                Dim ini As New IniFile(sFinditResultfile)
                Dim sStatus As String = ini.ReadValue("Status", "Status", "?")
                If sStatus = "Finished" Then
                    SMsgToUser = CheckFinditStatus()
                    If SMsgToUser = "0" Then
                        SMsgToUser = "Findit it had no results" & vbCrLf& & vbCrLf &
                        "please check the Directories you selected for search and the query built by the agent:" & vbCrLf &
                        "Is it possible you have not one file containing the searched words?" & vbCrLf &
                        "Maybee you should ask a question which does contain (part of) Words which are existant within the files."
                        CurrentAction = ChunkActionEnum.FinishedALL
                    Else
                        ' The search is complete; continue with reranking.
                        bFinditRunning = False
                        CurrentAction = ChunkActionEnum.Rerank
                    End If
                ElseIf sStatus = "Error" Then
                    bFinditRunning = False
                    CurrentAction = ChunkActionEnum.FinishedALL
                    Return "Findit reports an error: " & ini.ReadValue("Status", "Errortext", "") & vbCrLf
                End If
            End If
        End If
        ' Apply the latest reranking progress text to the protocol.
        If NewLineForRTF <> "" Then
            frmMain.UpdateLastProtocolLine(NewLineForRTF)
            NewLineForRTF = ""
        End If
        ' Finalize reranking after all batches have completed.
        ' Mark probable duplicates, persist the ranking and stop the reranker service.
        If CurrentAction = ChunkActionEnum.RerankCleanUp Then
            CurrentAction = ChunkActionEnum.RerankCleaning
            MarkProbableDuplicatesInRerankList()
            WriteRerankList()
            StopRerankerService()
            Return ""
        End If
        ' Start reading the highest-ranked files.
        If CurrentAction = ChunkActionEnum.ExcerptsNEXT Then
            If currentSession.Provider = "Ollama" Then
                SMsgToUser &= "Agent starts reading for excerpts"
            Else
                SMsgToUser &= "Agent starts reading"
            End If
            StartExcerptChain()
        End If
        ' Continue excerpting after the previous file has completed or been cancelled.
        If CurrentAction = ChunkActionEnum.ExcerptRESTART Then
            Dim displayQuestion As String = query.DetailQuestions(query.ExcerptQuestionIndex).Question
            Dim hits As List(Of RerankHit) = query.RerankResults(displayQuestion)
            Dim nextHitIndex As Integer = query.ExcerptHitIndex
            If query.ExcerptHitIndex > 5 AndAlso nextHitIndex < hits.Count AndAlso hits(nextHitIndex).Score < 0 Then
                ' No more sufficiently high-ranked files remain.
                CurrentAction = ChunkActionEnum.SynthesisSTART
                LogDebug(">>>> Ranking getting too low -> FinalSynthesis")
            ElseIf nextHitIndex >= hits.Count Then
                ' No more ranked files remain.
                CurrentAction = ChunkActionEnum.SynthesisSTART
                LogDebug(">>>> All rerank hits excerpted -> FinalSynthesis")
            Else
                ' Continue with the next ranked file.
                RunNextExcerptFromRerankResults()
            End If
        End If
        ' Cancel an excerpt request that has exceeded its timeout.
        If CurrentAction = ChunkActionEnum.ExcerptsRUN Then

            If query.ExcerptPromptStarted <> DateTime.MinValue AndAlso (DateTime.Now - query.ExcerptPromptStarted).TotalSeconds > 240 Then
                Dim elapsedSeconds As Double = (DateTime.Now - query.ExcerptPromptStarted).TotalSeconds
                LogDebug("######### >> Excerpt timeout after " & elapsedSeconds.ToString("0.0") & " s | Started=" & query.ExcerptPromptStarted.ToString("HH:mm:ss.fff"))
                CurrentAction = ChunkActionEnum.ExcerptCANCELLED
                If iPromptBalance > 0 Then Threading.Interlocked.Decrement(iPromptBalance)
                If frmMain.sendCancellation IsNot Nothing Then
                    frmMain.sendCancellation.Cancel()
                Else
                    LogDebug("######### >> sendCancellation was Nothing at excerpt timeout")
                End If

            End If
        End If
        ' Start synthesis from the collected excerpts.
        If CurrentAction = ChunkActionEnum.SynthesisSTART Then
            SMsgToUser = "Agent working on synthesis."
            CurrentAction = ChunkActionEnum.Synthesis
            Dim rMsg As String = StartSynthesisForCurrentExcerptFile()
            If rMsg <> "" Then
                SMsgToUser = rMsg
                CurrentAction = ChunkActionEnum.FinishedALL
            End If
        End If        ' Return any user-facing status text produced in this cycle.
        ' Keep protocol marker spacing readable.
        Return If(SMsgToUser, "").Replace("<|>", " <|> ")
    End Function

    ' Handles AI responses from the multi-step query-building workflow.
    ' Each Action value updates query state and schedules the next markdown prompt.
    Public Function QueryActions(Action As String, jResponse As JsonElement) As String
        Dim SMsgToUser As String = ""
        ' Validate the required JSON payload first.
        Dim fijProp As JsonElement
        If Not jResponse.TryGetProperty("Answer", fijProp) Then
            Return "Not valid json returned"
        End If
        ' The model result is carried in the Answer property.
        Dim Answer As String = If(fijProp.GetString(), "").Trim
        ' Advance the query-building state machine.
        Select Case Action
            Case "chunk_Query_0"
                ' Read the detected user language.
                If jResponse.TryGetProperty("Language", fijProp) Then
                    currentSession.Userlanguage = If(fijProp.GetString(), "").Trim
                End If
                LogDebug("################  detected Languate: " & currentSession.Userlanguage)
                ' Read an optional time restriction.
                If jResponse.TryGetProperty("Time", fijProp) Then
                    query.TimeRestriction = If(fijProp.GetString(), "").Trim
                End If
                If Answer = query.UserQuestion.Trim Then
                    ' No spelling correction was required.
                    SMsgToUser = "Start reasoning about this Question."
                Else
                    SMsgToUser = "Start reasoning about: " & Answer.Trim
                End If
                query.UserQuestion = Answer.Trim
                ' Determine whether this is a knowledge or statistical query.
                QueryPrompts("chunk_Query_0b")
            Case "chunk_Query_0b"
                ' Handle the detected query type.
                If jResponse.TryGetProperty("Answer", fijProp) Then
                    query.Type = If(fijProp.GetString(), "").Trim
                    If query.Type.ToLower.Contains("statistik") Then
                        ' Statistical extraction requires a separate workflow that is not implemented yet.
                        SMsgToUser = "This request requires a statistical extraction workflow." & vbCrLf & vbCrLf & "To answer it reliably, Findit6Agent would need to process every matching file and extract a short, structured data record from each one. The current beta version Is optimized for knowledge extraction: it relevance-ranks documents, reads the most relevant files in detail, and synthesizes factual answers from them." & vbCrLf & vbCrLf &
                                     "A dedicated statistical extraction branch is technically possible and already planned, but is not yet implemented in the current beta version. Since Findit6Agent is intended as an open-source project, this workflow may also be added or extended independently."
                        CurrentAction = ChunkActionEnum.FinishedALL
                    Else
                        IdentifyTechTerms(query.UserQuestion)
                        QueryPrompts("chunk_Query_1")
                    End If
                End If
            Case "chunk_Query_1"
                ' Store the identified core entity.
                query.CoreEntity = Answer.Replace(" / ", " <|> ")
                query.CoreEntity = RestoreTechnicalTermQuotes(query.CoreEntity)
                If query.CoreEntity.Contains("<|>") Or query.CoreEntity.Contains("< | >") Then
                    SMsgToUser = "Checking relationship: " & query.CoreEntity
                    QueryPrompts("chunk_Query_1b")
                Else
                    SMsgToUser = "Looking for aspects of: " & query.CoreEntity
                    QueryPrompts("chunk_Query_2")
                End If
                RenameSessionFolderWithCoreEntity()
                CurrentAction = ChunkActionEnum.WaitForNext
            Case "chunk_Query_1b"
                ' Store the clarified relation between multiple entities.
                query.CoreEntity = Answer.Trim
                query.CoreEntity = RestoreTechnicalTermQuotes(query.CoreEntity)
                SMsgToUser = "Looking for aspects of: " & query.CoreEntity
                ' Continue by identifying relevant aspects.
                QueryPrompts("chunk_Query_2")
            Case "chunk_Query_2"
                ' Store the identified core aspects.
                query.CoreAspects = Answer.Trim.Replace(" / ", " OR ")
                query.CoreAspects = RestoreTechnicalTermQuotes(query.CoreAspects)
                If query.CoreAspects.Contains("<|>") Then
                    ' Multiple aspects require a relation check.
                    SMsgToUser = "Checking aspect relation of: " & query.CoreAspects
                    QueryPrompts("chunk_Query_3") ' checking Aspect correlation
                ElseIf query.CoreAspects.Contains("NoAspects") Then
                    ' No additional aspects were found; continue building the query.
                    query.AIquestion = query.CoreEntity
                    SMsgToUser = "Building query: " & query.AIquestion
                    QueryPrompts("chunk_Query_5")
                Else
                    ' Remove soft aspects before final query construction.
                    query.AIquestion = query.CoreEntity & " AND " & query.CoreAspects
                    SMsgToUser = "Reasoning on soft aspects: " & query.AIquestion
                    QueryPrompts("chunk_Query_4")
                End If
            Case "chunk_Query_3"
                ' Store the resolved logical relation between aspects.
                query.CoreAspects = Answer.Trim.Replace("(", "").Replace(")", "")
                query.CoreAspects = RestoreTechnicalTermQuotes(query.CoreAspects)
                If query.CoreAspects.Trim <> "" Then
                    query.AIquestion = query.CoreEntity & " AND " & query.CoreAspects
                    SMsgToUser = "Reasoning on soft aspects: " & query.AIquestion
                    QueryPrompts("chunk_Query_4")
                Else
                    query.AIquestion = query.CoreEntity
                    query.AIquestion = RestoreSynonymsToTerm(query.AIquestion)
                    query.untruncated = query.AIquestion
                    SMsgToUser = "Checking for user rules: " & query.AIquestion
                    QueryPrompts("chunk_Query_UserRules")
                End If
            Case "chunk_Query_4"
                ' Apply the model result after soft-aspect filtering.
                Answer = CleanDeletedAspects(Answer.Replace("NoAspects", "").Trim)
                query.AIquestion = Answer.Trim
                Answer = RestoreTechnicalTermQuotes(query.AIquestion)
                query.AIquestion = RestoreSynonymsToTerm(query.AIquestion)
                query.untruncated = query.AIquestion
                SMsgToUser = "Checking for user rules: " & query.AIquestion
                QueryPrompts("chunk_Query_UserRules")
            Case "chunk_Query_UserRules"
                query.AIquestion = Answer.Trim.Replace("\*", "*")
                SMsgToUser = "Building Query: " & query.AIquestion
                QueryPrompts("chunk_Query_5")
            Case "chunk_Query_5"
                ' Protect names and prepare per-word truncation.
                query.AIquestion = Answer.Replace(" OR / OR ", " OR ")
                query.AIquestion = Answer.Replace("%#%", "#%#").Replace(" OR / OR ", " OR ")
                query.AIquestion = AddingLogicalOperators(query.AIquestion)
                query.AIquestion = RestoreTechnicalTermQuotes(query.AIquestion)
                Dim c As String = Chr(34)
                query.AIquestion = query.AIquestion.Replace(c, "")
                If OnlyNames(query.AIquestion) Then
                    SMsgToUser = "Agent started Findit6 with: " & vbCrLf & query.AIquestion
                    chunk_Start_Findit6()
                Else
                    query.TruncNumber = 1
                    query.TruncationList = PrepareTruncation(query.AIquestion)
                    SMsgToUser = "Working on truncation: " & query.AIquestion
                    QueryPrompts("chunk_Query_6")
                End If
            Case "chunk_Query_6"

                Dim wordTypeProp As JsonElement
                If Not jResponse.TryGetProperty("WordType", wordTypeProp) Then
                    LogDebug("[ERROR] chunk_Query_6: WordType missing in agent response.")
                    SMsgToUser = "Agent returned an invalid truncation response."
                    Exit Select
                End If
                Dim WordType As String = If(wordTypeProp.GetString(), "").Trim()
                Dim BaseForm As String = ""
                Dim baseFormProp As JsonElement
                If jResponse.TryGetProperty("BaseForm", baseFormProp) AndAlso baseFormProp.ValueKind = JsonValueKind.String Then
                    BaseForm = If(baseFormProp.GetString(), "").Trim()
                End If
                Dim item As TruncationItem = query.TruncationItems(query.TruncNumber - 1)
                item.WordType = WordType
                item.Baseform = BaseForm
                Dim wordForms As New List(Of String)
                For Each prop As JsonProperty In jResponse.EnumerateObject()
                    If prop.Name.Equals("Action", StringComparison.OrdinalIgnoreCase) OrElse prop.Name.Equals("Answer", StringComparison.OrdinalIgnoreCase) OrElse prop.Name.Equals("WordType", StringComparison.OrdinalIgnoreCase) Then Continue For
                    If prop.Value.ValueKind <> JsonValueKind.String Then Continue For
                    Dim wordForm As String = If(prop.Value.GetString(), "").Trim()
                    If wordForm <> "" Then wordForms.Add(wordForm)
                Next
                wordForms = wordForms.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                If wordForms.Count = 0 Then wordForms.Add(item.Original)
                LogDebug("<<<<<<<<<<<<<<<< Truncation: " & item.Original & " | " & WordType & " | " & String.Join(" | ", wordForms))
                Dim Trunkalized As String = Trunkalizer(wordForms.ToArray()).Trim(ControlChars.Quote)
                item.Result = Trunkalized
                If item.Result = "" Then item.Result = item.Baseform & "*"
                If query.TruncNumber < query.TruncationItems.Count Then
                    TruncationListWorker()
                Else
                    query.AIquestion = RestoreTruncationResults().Replace("**", "*")
                    SMsgToUser = "Reasoning about optimized trunkation: " & query.AIquestion
                    QueryPrompts("chunk_Query_7")
                End If
            Case "chunk_Query_7"
                ' Continue with the next query optimization step.
                query.AIquestion = Answer.Trim
                SMsgToUser = "Reasoning about helpful synonmys : " & query.AIquestion
                QueryPrompts("chunk_Query_8")
            Case "chunk_Query_8"
                ' Continue with the next query optimization step.
                query.AIquestion = EnsureOrGroupParentheses(Answer.Trim)
                SMsgToUser = "Agent started Findit6 with: " & vbCrLf & query.AIquestion
                chunk_Start_Findit6()
        End Select
        Return SMsgToUser
    End Function

    ' Loads and sends the markdown prompt for one query-building step.
    ' Local Ollama calls use step-specific mood settings; external providers use normal settings.
    Public Sub QueryPrompts(markdown As String)
        Dim LanguageMarkdowns As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
             "chunk_Query_6",  ' Truncations
             "chunk_Query_7",  ' Truncation refinement
             "chunk_Query_8"}  ' Synonyms
        Dim NoLanMasrkdowns As Boolean = Not LanguageMarkdowns.Any(Function(stamm) markdown.StartsWith(stamm, StringComparison.OrdinalIgnoreCase))
        Dim mdPath As String = ""
        If NoLanMasrkdowns Then
            mdPath = Path.Combine(AgentWorkspace, markdown) & ".md"
        Else
            LogDebug("######################### used Language: = " & currentSession.Userlanguage)
            mdPath = Path.Combine(AgentWorkspace, markdown) & currentSession.Userlanguage.Trim & ".md"
        End If
        Try
            ' Load the selected markdown and insert the current query values.
            If Not File.Exists(mdPath) Then
                Dim errorMessage As String = "[ERROR] Markdown not found: " & mdPath
                LogDebug(errorMessage)
                Exit Sub
            End If
            Dim prompt As String = LoadChunkMarkdown(mdPath)
            prompt = prompt.Replace("%UserFrage%", query.UserQuestion)
            prompt = prompt.Replace("%Userfrage%", query.UserQuestion)
            prompt = prompt.Replace("%CoreEntity%", query.CoreEntity)
            prompt = prompt.Replace("%CoreAspects%", query.CoreAspects)
            prompt = prompt.Replace("%Query%", query.AIquestion)
            prompt = prompt.Replace("%untruncatedQuery%", query.untruncated)
            prompt = prompt.Replace("%AktDateString%", Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            ' Small local models need strict instructions focused on what to do.
            ' Large external models may reason about why a step exists instead of simply executing it.
            ' For selected steps an additional explanation helps keep that reasoning on track.
            ' The same explanation can unnecessarily distract smaller local models.
            ' Most base markdown prompts work for both model classes without maintaining separate files.
            ' Optional big-model additions are therefore inserted only where needed.
            prompt = InsertBigModelAddition(prompt, markdown)
            If query.TruncNumber > 0 Then
                prompt = prompt.Replace("%WordToTruncate%", query.TruncationItems(query.TruncNumber - 1).Original)
            End If
            ' Send the completed markdown prompt.
            LogDebug(">>>>>>>>>>>>> Sending Markdown: " & mdPath)
            If currentSession.Provider = "Ollama" Then
                ' Local models use step-specific inference settings.
                ' The active mood controls reasoning, temperature and related inference settings.
                ' Settings depend on the markdown step being executed.
                ' Send the prompt with the selected local-model settings.
                If currentSession.UseThinking Then
                    frmMain.Oll_SendPromptAsync(prompt:=prompt, useHistory:=False, think:=True, keepAlive:="5m")
                Else
                    frmMain.Oll_SendPromptAsync(prompt:=prompt, useHistory:=False, think:=False, keepAlive:="5m")
                End If
            Else
                ' External providers use their normal provider settings.
                frmMain.SendPromptAsync(userPrompt:=prompt, clearHistory:=True, useGuidedSettings:=True)
            End If
        Catch ex As Exception
            Dim errorMessage As String = "[ERROR] sending prompt spellchecking: " & ex.Message
            LogDebug(errorMessage)
        End Try
    End Sub

    ' Adds an optional explanation block for larger external models.
    ' Local Ollama prompts remove the marker and keep the compact base instructions.
    Private Function InsertBigModelAddition(prompt As String, markdown As String) As String
        If currentSession.Provider = "Ollama" Then
            Return Regex.Replace(prompt, "##\*\*\d+\*\*##", "")
        End If
        Dim match As Match = Regex.Match(markdown, "(?<!\d)(\d+)(?:[A-Za-z]*)(?:\.md)?$", RegexOptions.IgnoreCase)
        If Not match.Success Then
            Return prompt
        End If
        Dim stepNumber As String = match.Groups(1).Value
        Dim marker As String = "##**" & stepNumber & "**##"
        Dim additionPath As String = Path.Combine(AgentWorkspace, "ChunkBigModel" & stepNumber & ".md")
        If Not prompt.Contains(marker) Then
            Return prompt
        End If
        If Not File.Exists(additionPath) Then
            Return prompt.Replace(marker, "")
        End If
        Dim addition As String = File.ReadAllText(additionPath, Encoding.UTF8).Trim()
        Return prompt.Replace(marker, addition)
    End Function

    ' Loads one Chunk Mode markdown together with its execution settings.
    ' The local metadata header is removed and is never sent to the AI.
    Private Function LoadChunkMarkdown(mdPath As String) As String
        Const marker As String = ">>>>>>>>>>>>> md start"

        If Not File.Exists(mdPath) Then Throw New FileNotFoundException("Markdown not found.", mdPath)

        ' Safe defaults keep the workflow running even with damaged metadata.
        Dim moodValue As AI_Mood = AI_Mood.Logical
        Dim thinkValue As Boolean = True
        Dim tempValue As Double = 0.5

        Dim mdIni As New IniFile(mdPath)
        Dim moodText As String = mdIni.ReadValue("Mood", "Mood", "").Trim()
        Dim thinkText As String = mdIni.ReadValue("Mood", "Think", "").Trim()
        Dim tempText As String = mdIni.ReadValue("Mood", "Temp", "").Trim()

        ' Determine the mood first because it selects the default temperature.
        If moodText = "" Then
            LogDebug("[WARN] Markdown Mood missing. Using Logical: " & mdPath)
        ElseIf Not System.Enum.TryParse(Of AI_Mood)(moodText, True, moodValue) Then
            LogDebug("[WARN] Invalid Markdown Mood '" & moodText & "'. Using Logical: " & mdPath)
            moodValue = AI_Mood.Logical
        ElseIf Not System.Enum.IsDefined(GetType(AI_Mood), moodValue) Then
            LogDebug("[WARN] Undefined Markdown Mood '" & moodText & "'. Using Logical: " & mdPath)
            moodValue = AI_Mood.Logical
        End If

        ' Thinking is controlled directly by the markdown.
        If thinkText = "" Then
            LogDebug("[WARN] Markdown Think missing. Using True: " & mdPath)
        ElseIf Not Boolean.TryParse(thinkText, thinkValue) Then
            LogDebug("[WARN] Invalid Markdown Think '" & thinkText & "'. Using True: " & mdPath)
            thinkValue = True
        End If

        ' Start with the mood temperature and optionally override it from the markdown.
        Dim moodTempText As String = AgentsIni.ReadValue(moodValue.ToString(), "Temp", "").Trim()
        If moodTempText = "" Then
            LogDebug("[WARN] Temp missing for mood '" & moodValue.ToString() & "'. Using 0.5.")
        ElseIf Not Double.TryParse(moodTempText, NumberStyles.Float, CultureInfo.InvariantCulture, tempValue) Then
            LogDebug("[WARN] Invalid Temp '" & moodTempText & "' for mood '" & moodValue.ToString() & "'. Using 0.5.")
            tempValue = 0.5
        End If

        If tempText <> "" Then
            Dim mdTempValue As Double
            If Double.TryParse(tempText, NumberStyles.Float, CultureInfo.InvariantCulture, mdTempValue) Then
                tempValue = mdTempValue
            Else
                LogDebug("[WARN] Invalid Markdown Temp '" & tempText & "'. Using mood default " & tempValue.ToString(CultureInfo.InvariantCulture) & ": " & mdPath)
            End If
        End If

        currentSession.AImood = moodValue
        currentSession.UseThinking = thinkValue
        currentSession.temp = tempValue

        Dim content As String = File.ReadAllText(mdPath, Encoding.UTF8)
        Dim markerPos As Integer = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase)

        If markerPos < 0 Then
            LogDebug("[WARN] Markdown start marker missing. Sending complete markdown: " & mdPath)
            Return content
        End If

        content = content.Substring(markerPos + marker.Length).TrimStart(ControlChars.Cr, ControlChars.Lf)

        LogDebug("[Markdown] Mood=" & currentSession.AImood.ToString() & " Think=" & currentSession.UseThinking.ToString() & " Temp=" & currentSession.temp.ToString(CultureInfo.InvariantCulture))
        Return content
    End Function

#End Region

#Region "Building Query in Detail"

#Region "Truncalizer"

    ' Hybrid linguistic truncation method
    ' Developed independently for Findit6Agent by Michael Houben, 2026.
    ' The method combines context-sensitive LLM-derived morphological and derivational word forms with deterministic common-prefix truncation,
    ' a minimum-prefix safety threshold, and Or fallback when no sufficiently safe common prefix exists.
    ' The idea may be reused freely; attribution Is appreciated.

    ' Useful wildcard truncation is difficult, especially for German inflection.
    ' Small local models were not reliable enough to perform the complete transformation directly.
    ' The model therefore returns related word forms such as singular, plural , nouns and verb base forms.
    ' Shared prefixes can then be converted into deterministic wildcard expressions.
    ' Examples: Windrad/Windräder, Frau/Frauen and Haus/Häuser may require different wildcard forms
    ' or must be splittet with or: "Haus* OR 'Häuser' 

    ' Names, NEAR expressions and technical terms must remain protected during this process.

    ' Checks whether the query consists only of protected NEAR name expressions.
    ' Such queries can skip the normal per-word truncation workflow.
    Private Function OnlyNames(query As String) As Boolean
        If String.IsNullOrWhiteSpace(query) Then Return False
        Dim groups As String() = Regex.Split(query.Trim(), "\s+AND\s+", RegexOptions.IgnoreCase)
        If groups.Length = 0 Then Return False
        For Each group As String In groups
            If Not Regex.IsMatch(group, "\s+NEAR\s+", RegexOptions.IgnoreCase) Then Return False
            If Regex.IsMatch(group, "\s+OR\s+", RegexOptions.IgnoreCase) Then Return False
        Next
        Return True
    End Function

    ' Extracts truncatable terms into a placeholder-backed work list.
    ' Protected NEAR expressions and quoted technical terms remain unchanged.
    Public Function PrepareTruncation(sourceQuery As String) As String
        query.TruncationItems.Clear()
        query.TruncationTemplate = sourceQuery
        Dim protectedRanges As New List(Of Tuple(Of Integer, Integer))
        Dim wordPattern As String = "[\p{L}\p{M}\p{N}]+(?:[-/][\p{L}\p{M}\p{N}]+)*"
        Dim techTermPattern As String = """[^""]+"""
        Dim truncationPattern As String = techTermPattern & "|" & wordPattern
        Dim nearPattern As String = wordPattern & "\s+NEAR\s+" & wordPattern
        For Each m As Match In Regex.Matches(sourceQuery, nearPattern, RegexOptions.IgnoreCase)
            protectedRanges.Add(Tuple.Create(m.Index, m.Index + m.Length))
        Next
        query.TruncationTemplate = Regex.Replace(sourceQuery, truncationPattern, New MatchEvaluator(
        Function(m As Match) As String
            If IsProtectedPosition(m.Index, protectedRanges) Then Return m.Value
            If m.Value.Equals("AND", StringComparison.OrdinalIgnoreCase) Then Return m.Value
            If m.Value.Equals("OR", StringComparison.OrdinalIgnoreCase) Then Return m.Value
            If m.Value.Equals("NEAR", StringComparison.OrdinalIgnoreCase) Then Return m.Value
            Dim placeholder As String = "§T" & query.TruncationItems.Count & "§"
            query.TruncationItems.Add(New TruncationItem With {.Placeholder = placeholder, .Original = m.Value})
            Return placeholder
        End Function))
        Return String.Join(" <|> ", query.TruncationItems.Select(Function(x) x.Original))
    End Function

    ' Advances to the next pending truncation item.
    ' The selected word is exposed through query state before the next prompt is sent.
    Public Sub TruncationListWorker()
        If query.TruncationItems.Count = 0 Then Exit Sub
        If query.TruncNumber >= query.TruncationItems.Count Then Exit Sub
        query.TruncAct = query.TruncationItems(query.TruncNumber).Original
        query.TruncNumber += 1
        QueryPrompts("chunk_Query_6")
    End Sub

    ' Checks whether a text position falls inside one of the protected ranges.
    ' Protected positions are skipped while building the truncation template.
    Private Function IsProtectedPosition(position As Integer, ranges As List(Of Tuple(Of Integer, Integer))) As Boolean
        For Each range In ranges
            If position >= range.Item1 AndAlso position < range.Item2 Then Return True
        Next
        Return False
    End Function

    ' Builds a wildcard search expression from related word forms.
    ' A sufficiently long shared prefix is preferred; otherwise separate OR forms are returned.
    Public Function Trunkalizer(ParamArray wordForms() As String) As String
        Dim words As New List(Of String)
        For Each word As String In wordForms
            If Not String.IsNullOrWhiteSpace(word) Then words.Add(word.Trim())
        Next
        words = words.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        If words.Count = 0 Then Return ""
        If words.Count = 1 Then Return words(0) & "*"
        Dim commonPrefix As String = words(0)
        For i As Integer = 1 To words.Count - 1
            commonPrefix = GetCommonPrefix(commonPrefix, words(i))
            If commonPrefix = "" Then Exit For
        Next
        If commonPrefix.Length > 4 Then Return commonPrefix & "*"
        Dim results As New List(Of String)
        For Each word As String In words
            results.Add(word & "*")
        Next
        Return String.Join(" OR ", results)
    End Function

    ' Wraps top-level OR groups so their meaning survives surrounding AND operators.
    ' Already parenthesized groups are preserved unchanged.
    Private Function EnsureOrGroupParentheses(query As String) As String
        If String.IsNullOrWhiteSpace(query) Then Return query
        Dim groups As New List(Of String)
        Dim start As Integer = 0
        Dim depth As Integer = 0
        For i As Integer = 0 To query.Length - 1
            If query(i) = "("c Then depth += 1
            If query(i) = ")"c Then depth -= 1
            If depth = 0 AndAlso i + 4 <= query.Length AndAlso String.Compare(query, i, " AND", 0, 4, True) = 0 Then
                groups.Add(query.Substring(start, i - start).Trim())
                start = i + 5
                i += 4
            End If
        Next
        groups.Add(query.Substring(start).Trim())
        For i As Integer = 0 To groups.Count - 1
            If ContainsTopLevelOr(groups(i)) AndAlso Not HasOuterParentheses(groups(i)) Then groups(i) = "(" & groups(i) & ")"
        Next
        Return String.Join(" AND ", groups)
    End Function

    ' Detects whether an expression contains an OR operator at nesting level zero.
    ' OR operators inside parentheses do not count.
    Private Function ContainsTopLevelOr(text As String) As Boolean
        Dim depth As Integer = 0
        For i As Integer = 0 To text.Length - 1
            If text(i) = "("c Then depth += 1
            If text(i) = ")"c Then depth -= 1
            If depth = 0 AndAlso i + 4 <= text.Length AndAlso String.Compare(text, i, " OR ", 0, 4, True) = 0 Then Return True
        Next
        Return False
    End Function

    ' Checks whether one outer parenthesis pair encloses the complete expression.
    ' Nested internal parentheses are allowed.
    Private Function HasOuterParentheses(text As String) As Boolean
        text = text.Trim()
        If text.Length < 2 OrElse text(0) <> "("c OrElse text(text.Length - 1) <> ")"c Then Return False
        Dim depth As Integer = 0
        For i As Integer = 0 To text.Length - 1
            If text(i) = "("c Then depth += 1
            If text(i) = ")"c Then depth -= 1
            If depth = 0 AndAlso i < text.Length - 1 Then Return False
        Next
        Return depth = 0
    End Function

    ' Returns the case-insensitive common prefix shared by two words.
    ' The original casing of the first word is retained.
    Private Function GetCommonPrefix(firstWord As String, secondWord As String) As String
        Dim maxLength As Integer = Math.Min(firstWord.Length, secondWord.Length)
        Dim i As Integer = 0
        While i < maxLength AndAlso Char.ToLowerInvariant(firstWord(i)) = Char.ToLowerInvariant(secondWord(i))
            i += 1
        End While
        Return firstWord.Substring(0, i)
    End Function

    ' Replaces truncation placeholders with their completed wildcard expressions.
    ' OR results are parenthesized when the surrounding query does not already provide grouping.
    Public Function RestoreTruncationResults() As String
        Dim finalQuery As String = query.TruncationTemplate
        For Each item As TruncationItem In query.TruncationItems
            If String.IsNullOrWhiteSpace(item.Result) Then Throw New InvalidOperationException("Missing truncation result for: " & item.Original)
            Dim replacement As String = item.Result.Trim().Trim(ControlChars.Quote).Trim()
            If replacement.Contains(" OR ") AndAlso Not PlaceholderIsInsideOrGroup(finalQuery, item.Placeholder) Then
                replacement = "(" & replacement & ")"
            End If
            finalQuery = finalQuery.Replace(item.Placeholder, replacement)
        Next
        Return finalQuery
    End Function

    ' Checks whether a truncation placeholder already sits inside an OR group.
    ' This prevents redundant parentheses when restoring truncation results.
    Private Function PlaceholderIsInsideOrGroup(ByVal fullQuery As String, ByVal placeholder As String) As Boolean
        Dim pos As Integer = fullQuery.IndexOf(placeholder, StringComparison.Ordinal)
        If pos < 0 Then Return False
        Dim level As Integer = 0
        Dim openPos As Integer = -1
        For i As Integer = pos - 1 To 0 Step -1
            If fullQuery(i) = ")"c Then
                level += 1
            ElseIf fullQuery(i) = "("c Then
                If level = 0 Then
                    openPos = i
                    Exit For
                End If
                level -= 1
            End If
        Next
        If openPos < 0 Then Return False
        level = 0
        Dim closePos As Integer = -1
        For i As Integer = openPos To fullQuery.Length - 1
            If fullQuery(i) = "("c Then
                level += 1
            ElseIf fullQuery(i) = ")"c Then
                level -= 1
                If level = 0 Then
                    closePos = i
                    Exit For
                End If
            End If
        Next
        If closePos < 0 Then Return False
        Dim innerGroup As String = fullQuery.Substring(openPos + 1, closePos - openPos - 1)
        Return SplitTopLevelOperator(innerGroup, "OR").Count > 1
    End Function

#End Region

#Region "Synonyms and Technical Terms"

    ' Small models are not consistently reliable at generating all useful synonyms.
    ' User-supplied synonym groups therefore remain deterministic input to the workflow.
    ' Synonyms can be supplied directly in double parentheses after a word.
    ' Example: WindTurbine ((wind farm)) is extracted before the model processes the question.
    ' The stored synonym expression is restored after the model transformation.
    ' This keeps user-defined alternatives intact across query-building steps.
    ' Technical terms are protected for the same reason: "heat pump" must not become Heat OR Pump.
    ' Protected technical terms are also excluded from truncation.

    ' Extracts user-defined synonym groups from the original question.
    ' The cleaned question keeps only the original word while synonyms are stored for later restoration.

    Public Function ExtractSynonymsFromQuestion(question As String) As String
        query.Synonyms.Clear()
        If String.IsNullOrWhiteSpace(question) Then Return question

        Dim pattern As String = "(?<word>[\p{L}\p{N}][\p{L}\p{N}\-_]*)\s*\(\((?<content>[^()]*)\)\)"
        Dim cleanedQuestion As String = Regex.Replace(question, pattern,
        Function(match As Match) As String
            Dim originalWord As String = match.Groups("word").Value
            Dim rawContent As String = match.Groups("content").Value.Trim()
            Dim cleanedContent As String = CleanSynonymContent(rawContent)

            If cleanedContent <> "" Then
                query.Synonyms.Add(New SynonymEntry With {.OriginalWord = originalWord, .RawContent = rawContent, .SynonymWords = cleanedContent})
            End If

            Return originalWord
        End Function)

        Return Regex.Replace(cleanedQuestion, "\s+", " ").Trim()
    End Function

    ' Normalizes user-supplied synonym text to the words needed by the search query.
    ' Parentheses, separators and textual OR variants are removed.
    Private Function CleanSynonymContent(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return ""
        End If
        Dim result As String = value
        result = result.Replace("(", " ")
        result = result.Replace(")", " ")
        result = result.Replace("/", " ")
        result = result.Replace(",", " ")
        result = Regex.Replace(result, "\b(?:or|oder)\b", " ", RegexOptions.IgnoreCase)
        result = Regex.Replace(result, "\s+", " ").Trim()
        Return result
    End Function

    ' Restores all stored synonym expressions to a transformed query term.
    ' Missing or empty synonym entries are ignored.
    Public Function RestoreSynonymsToTerm(term As String) As String
        If String.IsNullOrWhiteSpace(term) Then
            Return term
        End If
        If query.Synonyms Is Nothing OrElse query.Synonyms.Count = 0 Then
            Return term
        End If
        Dim result As String = term
        For Each entry As SynonymEntry In query.Synonyms
            If String.IsNullOrWhiteSpace(entry.OriginalWord) OrElse String.IsNullOrWhiteSpace(entry.SynonymWords) Then
                Continue For
            End If
            result = InsertSynonymsAfterWord(result, entry.OriginalWord, entry.SynonymWords)
        Next
        Return result
    End Function

    ' Inserts one synonym expression directly after its original word.
    ' Existing expansions are detected so the same synonym group is not added twice.
    Private Function InsertSynonymsAfterWord(text As String, originalWord As String, synonymWords As String) As String
        Dim pattern As String = "(?<![\p{L}\p{N}_])" & Regex.Escape(originalWord) & "(?![\p{L}\p{N}_])"
        Dim foundMatch As Match = Regex.Match(text, pattern, RegexOptions.IgnoreCase)
        If Not foundMatch.Success Then
            Return text
        End If
        ' Prevent inserting the same synonym expansion twice.
        Dim completePattern As String = pattern & "\s+" & BuildFlexibleWhitespacePattern(synonymWords)
        If Regex.IsMatch(text, completePattern, RegexOptions.IgnoreCase) Then Return text
        Dim insertPosition As Integer = foundMatch.Index + foundMatch.Length
        Return text.Insert(insertPosition, " " & synonymWords)
    End Function

    ' Builds a regex pattern that tolerates variable whitespace between words.
    ' Each literal word is escaped before the pattern is assembled.
    Private Function BuildFlexibleWhitespacePattern(expression As String) As String
        Dim words As String() = Regex.Split(expression.Trim(), "\s+")
        Return String.Join("\s+", words.Where(Function(word) word <> "").Select(Function(word) Regex.Escape(word)))
    End Function

    ' Collects quoted technical terms from the source question.
    ' Straight, German and English typographic quotation pairs are recognized.
    Public Sub IdentifyTechTerms(sourceQuestion As String)
        query.TechTerms.Clear()
        If String.IsNullOrWhiteSpace(sourceQuestion) Then Exit Sub
        Dim closingQuote As Char = ChrW(0)
        Dim termStart As Integer = -1
        Dim insideTerm As Boolean = False
        For i As Integer = 0 To sourceQuestion.Length - 1
            Dim currentChar As Char = sourceQuestion(i)
            Dim charCode As Integer = AscW(currentChar)
            If Not insideTerm Then
                Select Case charCode
                    Case 34
                        ' Straight quotation marks:
                        ' "heat pump"
                        closingQuote = ChrW(34)
                        termStart = i + 1
                        insideTerm = True
                    Case &H201E
                        ' German opening quotation mark:
                        ' Unicode U+201E
                        closingQuote = ChrW(&H201C)
                        termStart = i + 1
                        insideTerm = True
                    Case &H201C
                        ' English opening quotation mark:
                        ' Unicode U+201C
                        closingQuote = ChrW(&H201D)
                        termStart = i + 1
                        insideTerm = True
                End Select
            ElseIf currentChar = closingQuote Then
                Dim termLength As Integer = i - termStart
                If termLength > 0 Then
                    Dim term As String = sourceQuestion.Substring(termStart, termLength).Trim()
                    If term <> "" Then
                        Dim alreadyExists As Boolean = query.TechTerms.Any(Function(existingTerm As String) _
                                    String.Equals(existingTerm, term, StringComparison.OrdinalIgnoreCase))
                        If Not alreadyExists Then query.TechTerms.Add(term)
                    End If
                End If
                insideTerm = False
                closingQuote = ChrW(0)
                termStart = -1
            End If
        Next
    End Sub

    ' Restores quotation marks around all known technical terms.
    ' Case-insensitive replacements keep multi-word terms protected in later query steps.
    Private Function RestoreTechnicalTermQuotes(value As String) As String
        For Each techTerm As String In query.TechTerms
            Dim cleanTerm As String = techTerm.Trim().Trim(ControlChars.Quote)
            value = value.Replace(ControlChars.Quote & cleanTerm & ControlChars.Quote, cleanTerm, StringComparison.OrdinalIgnoreCase)
            value = value.Replace(cleanTerm, ControlChars.Quote & cleanTerm & ControlChars.Quote, StringComparison.OrdinalIgnoreCase)
        Next
        Return value
    End Function

#End Region

#Region "Search Term Construction"

    ' Builds explicit AND, OR and NEAR structure around the generated search terms.
    ' Protected expressions and technical terms are hidden temporarily while operators are inserted.
    Public Function AddingLogicalOperators(sourceQuery As String) As String
        If String.IsNullOrWhiteSpace(sourceQuery) Then Return ""
        Dim workingQuery As String = sourceQuery.Trim()
        Dim protectedTerms As New Dictionary(Of String, String)
        Dim protectedIndex As Integer = 0
        workingQuery = Regex.Replace(workingQuery, "#%#(.*?)#%#",
            Function(match As Match) As String
                Dim originalTerm As String = match.Groups(1).Value.Trim()
                Dim placeholder As String = "ZZPROTECTED" & protectedIndex.ToString("0000") & "ZZ"
                protectedIndex += 1
                protectedTerms.Add(placeholder, BuildNearExpression(originalTerm))
                Return placeholder
            End Function, RegexOptions.Singleline)
        workingQuery = workingQuery.Replace("<|>", " AND ")
        workingQuery = Regex.Replace(workingQuery, "\s+AND\s+", " AND ", RegexOptions.IgnoreCase)
        Dim rawGroups As String() = Regex.Split(workingQuery, "\s+AND\s+", RegexOptions.IgnoreCase)
        Dim finishedGroups As New List(Of String)
        For Each rawGroup As String In rawGroups
            Dim groupText As String = RemoveOuterParentheses(rawGroup.Trim())
            If groupText = "" Then Continue For
            Dim hiddenTechTerms As New Dictionary(Of String, String)
            groupText = HideTechTerms(groupText, hiddenTechTerms)
            groupText = InsertMissingOrOperators(groupText)
            groupText = RestoreTechTerms(groupText, hiddenTechTerms)
            For Each protectedTerm As KeyValuePair(Of String, String) In protectedTerms
                groupText = groupText.Replace(protectedTerm.Key, protectedTerm.Value, StringComparison.Ordinal)
            Next
            groupText = groupText.Trim()
            If groupText.Contains(" OR ", StringComparison.OrdinalIgnoreCase) OrElse groupText.Contains(" NEAR ", StringComparison.OrdinalIgnoreCase) Then
                finishedGroups.Add("(" & groupText & ")")
            Else
                finishedGroups.Add(groupText)
            End If
        Next
        Return String.Join(" AND ", finishedGroups)
    End Function

    ' Converts a protected multi-word term into a NEAR expression.
    ' Whitespace is normalized before joining the individual words.
    Private Function BuildNearExpression(term As String) As String
        If String.IsNullOrWhiteSpace(term) Then
            Return ""
        End If
        Dim words As String() = Regex.Split(term.Trim(), "\s+")
        Return String.Join(" NEAR ", words)
    End Function

    ' Inserts OR between adjacent terms that have no explicit operator.
    ' Existing OR and NEAR operators are retained.
    Private Function InsertMissingOrOperators(groupText As String) As String
        If String.IsNullOrWhiteSpace(groupText) Then
            Return ""
        End If
        Dim normalizedText As String = Regex.Replace(groupText.Trim(), "\s+", " ")
        Dim tokens As String() = normalizedText.Split(" "c, StringSplitOptions.RemoveEmptyEntries)
        Dim result As New List(Of String)
        Dim previousWasTerm As Boolean = False
        For Each rawToken As String In tokens
            Dim token As String = rawToken.Trim()
            If token = "" Then Continue For
            Dim isOperator As Boolean = token.Equals("OR", StringComparison.OrdinalIgnoreCase) OrElse token.Equals("NEAR", StringComparison.OrdinalIgnoreCase)
            If isOperator Then
                ' Keep existing operators unchanged.
                If result.Count > 0 AndAlso Not result.Last().Equals(token, StringComparison.OrdinalIgnoreCase) Then
                    result.Add(token.ToUpperInvariant())
                End If
                previousWasTerm = False
            Else
                ' Insert OR between adjacent ordinary terms.
                If previousWasTerm Then
                    result.Add("OR")
                End If
                result.Add(token)
                previousWasTerm = True
            End If
        Next
        Return String.Join(" ", result)
    End Function

    ' Removes DELETED placeholders returned by soft-aspect filtering.
    ' Remaining OR groups are rebuilt without changing unrelated query groups.
    Private Function CleanDeletedAspects(ByVal fullQuery As String) As String
        If String.IsNullOrWhiteSpace(fullQuery) Then Return ""
        Dim cleanedGroups As New List(Of String)
        For Each originalGroup As String In SplitTopLevelOperator(fullQuery.Trim(), "AND")
            Dim group As String = originalGroup.Trim()
            If group = "" Then Continue For
            Dim innerGroup As String = RemoveOuterParentheses(group).Trim()
            ' A single DELETED represents a complete soft AND aspect.
            If Regex.IsMatch(innerGroup, "^(?:DELETED)(?:\s+DELETED)*$", RegexOptions.IgnoreCase) Then Continue For
            Dim orParts As List(Of String) = SplitTopLevelOperator(innerGroup, "OR")
            If orParts.Count > 1 Then
                Dim remainingParts As List(Of String) = orParts.Where(Function(part) Not RemoveOuterParentheses(part.Trim()).Equals("DELETED", StringComparison.OrdinalIgnoreCase)).Select(Function(part) part.Trim()).ToList()
                ' DELETED + only one real alternative means the complete soft aspect is removed.
                If remainingParts.Count = 1 AndAlso remainingParts.Count < orParts.Count Then Continue For
                ' With several real alternatives, remove only DELETED.
                If remainingParts.Count < orParts.Count Then
                    Dim rebuiltGroup As String = String.Join(" OR ", remainingParts)
                    If group.StartsWith("(") AndAlso group.EndsWith(")") Then rebuiltGroup = "(" & rebuiltGroup & ")"
                    cleanedGroups.Add(rebuiltGroup)
                    Continue For
                End If
            End If
            cleanedGroups.Add(group)
        Next
        Return String.Join(" AND ", cleanedGroups)
    End Function

    ' Splits an expression on one logical operator at nesting level zero.
    ' Operators inside parentheses remain part of their original group.
    Private Function SplitTopLevelOperator(ByVal text As String, ByVal operatorWord As String) As List(Of String)
        Dim result As New List(Of String)
        If String.IsNullOrWhiteSpace(text) Then Return result
        Dim depth As Integer = 0
        Dim partStart As Integer = 0
        Dim i As Integer = 0
        While i < text.Length
            Select Case text(i)
                Case "("c
                    depth += 1
                Case ")"c
                    If depth > 0 Then depth -= 1
            End Select
            If depth = 0 AndAlso IsOperatorAt(text, i, operatorWord) Then
                result.Add(text.Substring(partStart, i - partStart).Trim())
                i += operatorWord.Length
                partStart = i
            Else
                i += 1
            End If
        End While
        result.Add(text.Substring(partStart).Trim())
        Return result
    End Function

    ' Checks whether an operator begins at the requested text position.
    ' Whitespace boundaries prevent partial-word matches.
    Private Function IsOperatorAt(ByVal text As String, ByVal index As Integer, ByVal operatorWord As String) As Boolean
        If index < 0 OrElse index + operatorWord.Length > text.Length Then Return False
        If String.Compare(text, index, operatorWord, 0, operatorWord.Length, StringComparison.OrdinalIgnoreCase) <> 0 Then Return False
        Dim validLeftBoundary As Boolean = index = 0 OrElse Char.IsWhiteSpace(text(index - 1))
        Dim rightIndex As Integer = index + operatorWord.Length
        Dim validRightBoundary As Boolean = rightIndex >= text.Length OrElse Char.IsWhiteSpace(text(rightIndex))
        Return validLeftBoundary AndAlso validRightBoundary
    End Function

    ' Removes redundant outer parenthesis pairs around a complete expression.
    ' Parentheses that cover only part of the expression are preserved.
    Private Function RemoveOuterParentheses(text As String) As String
        Dim result As String = text.Trim()
        Do While result.Length >= 2 AndAlso result.StartsWith("(", StringComparison.Ordinal) AndAlso result.EndsWith(")", StringComparison.Ordinal) AndAlso OuterParenthesesEncloseWholeExpression(result)
            result = result.Substring(1, result.Length - 2).Trim()
        Loop
        Return result
    End Function

    ' Checks whether the first and last parentheses enclose the whole expression.
    ' Unbalanced or prematurely closed groups return False.
    Private Function OuterParenthesesEncloseWholeExpression(text As String) As Boolean
        Dim depth As Integer = 0
        For i As Integer = 0 To text.Length - 1
            Select Case text(i)
                Case "("c
                    depth += 1
                Case ")"c
                    depth -= 1
                    If depth = 0 AndAlso i < text.Length - 1 Then
                        Return False
                    End If
                    If depth < 0 Then
                        Return False
                    End If
            End Select
        Next
        Return depth = 0
    End Function

    ' Replaces known technical terms with temporary placeholders.
    ' Longer terms are processed first to avoid premature partial matches.
    Private Function HideTechTerms(text As String, replacements As Dictionary(Of String, String)) As String
        Dim result As String = text
        Dim index As Integer = 0
        ' Process longer technical terms first so shorter partial matches cannot win prematurely.
        For Each techTerm As String In query.TechTerms.OrderByDescending(Function(term As String) term.Length)
            If String.IsNullOrWhiteSpace(techTerm) Then Continue For
            Dim placeholder As String = "ZZTECHTERM" & index.ToString("0000") & "ZZ"
            Dim pattern As String = Regex.Escape(techTerm.Trim()).Replace("\ ", "\s+")
            If Regex.IsMatch(result, pattern, RegexOptions.IgnoreCase) Then
                result = Regex.Replace(result, pattern, placeholder, RegexOptions.IgnoreCase)
                replacements.Add(placeholder, techTerm.Trim())
                index += 1
            End If
        Next
        Return result
    End Function

    ' Restores technical terms previously replaced by placeholders.
    ' Only placeholders collected for the current expression are processed.
    Private Function RestoreTechTerms(text As String, replacements As Dictionary(Of String, String)) As String
        Dim result As String = text
        For Each replacement As KeyValuePair(Of String, String) In replacements
            result = result.Replace(replacement.Key, replacement.Value)
        Next
        Return result
    End Function

#End Region

#End Region

#Region "Findit Search"
    ' Start Findit6 and prepare the result file for the reranking stage.

    ' Creates the session FIJ file and starts the Findit6 search.
    ' Reranker detail questions are prepared in parallel while Findit6 is running.
    Public Sub chunk_Start_Findit6()
        Dim sourceFile As String = Path.Combine(AgentWorkspace, "Standard.fij")
        Dim targetFile As String = Path.Combine(SessionWorkspace, "PrimerySearch.fij")
        sFinditResultfile = Path.Combine(SessionWorkspace, "PrimerySearch.fil")
        IO.File.Copy(sourceFile, targetFile, overwrite:=True)
        Dim fijPath As String = targetFile
        ' Treat the FIJ file as an INI-style configuration file.
        Dim ini As New IniFile(targetFile)
        ' Write the generated full-text search expression.
        ini.WriteValue("Fulltext", "SearchString", query.AIquestion)
        ' Apply the optional modification-date restriction.
        If query.TimeRestriction <> "" Then
            ' Use modification-date mode.
            ini.WriteValue("Date", "Mode", "2")
            ' Write the lower date limit.
            ini.WriteValue("Date", "After", query.TimeRestriction)
        Else
            ' Clear any date restriction inherited from the template.
            ini.WriteValue("Date", "Mode", "0")
            ini.WriteValue("Date", "After", "")
        End If
        ' Mark the search as running.
        bFinditRunning = True
        CurrentAction = ChunkActionEnum.FinditRUNNING
        ' Launch the FIJ file through the registered Findit6 handler.
        Dim procInfo As New System.Diagnostics.ProcessStartInfo()
        procInfo.FileName = fijPath
        procInfo.UseShellExecute = True
        System.Diagnostics.Process.Start(procInfo)
        ' Build the reranker questions in parallel with the Findit6 search.
        IdentifyQuestions()
        ' The caller will report progress while the search runs.
    End Sub

    ' Reads the completed Findit6 result status and basic search statistics.
    ' Successful searches advance the workflow to reranking and return a linked status message.
    Public Function CheckFinditStatus() As String
        Threading.Thread.Sleep(100)
        Dim ini As New IniFile(sFinditResultfile)
        If ini.ReadValue("Status", "Status", "Error") = "Error" Then
            CurrentAction = ChunkActionEnum.FinishedALL
            Return ini.ReadValue("Status", "Errortext", "") & vbCrLf
        End If
        FinditResults.SetPath(sFinditResultfile)
        iFinditFoundFiles = CInt(FinditResults.ReadValue("Statistik", "HitCount", "0"))
        If iFinditFoundFiles = 0 Then Return "0"
        Dim stringFilecount As String = FinditResults.ReadValue("Statistik", "AllFilesCount", "0")
        Dim iFinditSearchedFiles As Long = CLng(stringFilecount)
        Dim sFinditVolume = FinditResults.ReadValue("Statistik", "AllFilesSize", "0")
        Dim finditVolumeGB As Double = CDbl(sFinditVolume) / 1024 / 1024 / 1024
        CurrentAction = ChunkActionEnum.Rerank
        ' Return a protocol message containing a link to the result file.
        Dim linkUrl As String = New Uri(sFinditResultfile).AbsoluteUri
        Dim displayText As String = Path.GetFileName(sFinditResultfile)
        Return " Findit6 found " & iFinditFoundFiles.ToString() &
       " results in " & iFinditSearchedFiles.ToString("N0", CultureInfo.CurrentCulture) &
       " searched files with " & finditVolumeGB.ToString("N1", CultureInfo.CurrentCulture) & " GB" &
       " in: <|FILELINK|>" & linkUrl & "<|DISPLAY|>" & displayText & "<|ENDLINK|>" &
       vbCrLf & vbCrLf & "Start Reranking"
    End Function

    ' Asks the model to split the user question into reranker detail questions.
    ' The prompt is sent without previous chat history.
    Public Sub IdentifyQuestions()
        Try
            Dim mdFile As String = Path.Combine(AgentWorkspace, "Chunk_Identify_Questions.md")
            If Not File.Exists(mdFile) Then
                LogDebug("[ERROR] IdentifyQuestions: MD file not found: " & mdFile)
                Exit Sub
            End If
            Dim sprompt As String = LoadChunkMarkdown(mdFile)
            sprompt = sPrompt.Replace("%Userfrage%", query.UserQuestion)
            LogDebug("-----------> IdentifyQuestions")
            LogDebug("IdentifyQuestions prompt length: " & sPrompt.Length.ToString())
            ' Set the mood and prompt
            frmMain.SendPromptAsync(sprompt, False, True, useGuidedSettings:=True)
        Catch ex As Exception
            LogDebug("[ERROR] IdentifyQuestions: " & ex.Message)
            MsgBoxNeu("Unable to identify detail questions:" & vbCrLf & ex.Message, MsgBoxStyle.OkOnly)
        End Try
    End Sub

    ' Parses returned detail questions and stores their display and rerank forms.
    ' The formatted question list is returned for protocol display.
    Public Function QuestionsReturned(jResponse As JsonElement) As String
        Dim questionsProp As JsonElement
        If jResponse.TryGetProperty("questions", questionsProp) AndAlso questionsProp.ValueKind = JsonValueKind.Array Then
            ' First pass: parse all entries into DetailQuestions.
            For Each q As JsonElement In questionsProp.EnumerateArray()
                If q.ValueKind = JsonValueKind.String Then
                    Dim rawText As String = q.GetString()
                    If Not String.IsNullOrWhiteSpace(rawText) Then
                        Dim item As DetailQuestionItem = ParseDetailQuestionItem(rawText)
                        If Not String.IsNullOrWhiteSpace(item.Question) Then
                            query.DetailQuestions.Add(item)
                        End If
                    End If
                End If
            Next
            Dim sb As New StringBuilder()
            If query.DetailQuestions.Count = 1 Then
                sb.AppendLine("Agent identified rerank-question: ")
            Else
                sb.AppendLine("Agent identified rerank-questions: ")
            End If
            ' Second pass: append the parsed questions for display.
            Dim i As Integer = 0
            For Each item As DetailQuestionItem In query.DetailQuestions
                i += 1
                sb.AppendLine(i.ToString() & ". " & item.RerankQuery)
            Next
            LogDebug("DetailQuestions received: " & query.DetailQuestions.Count.ToString())
            If Not bFinditRunning Then sb.AppendLine(vbCrLf & vbCrLf & "Starting Rerank Batches:")
            Return sb.ToString()
        Else
            LogDebug("[ERROR] detailQuestions: questions array missing or invalid")
            Return "Unable to extract detail questions from JSON."
        End If
    End Function

    ' Parses one QUESTION/RERANK pair returned by the model.
    ' Plain text falls back to the same value for both fields.
    Private Function ParseDetailQuestionItem(raw As String) As DetailQuestionItem
        Dim item As New DetailQuestionItem()
        If String.IsNullOrWhiteSpace(raw) Then Return item
        Dim text As String = raw.Replace(vbCrLf, vbLf).Trim()
        Dim questionPos As Integer = text.IndexOf("QUESTION:", StringComparison.OrdinalIgnoreCase)
        Dim rerankPos As Integer = text.IndexOf("RERANK:", StringComparison.OrdinalIgnoreCase)
        If questionPos >= 0 AndAlso rerankPos > questionPos Then
            item.Question = text.Substring(questionPos + "QUESTION:".Length, rerankPos - questionPos - "QUESTION:".Length).Trim()
            item.RerankQuery = text.Substring(rerankPos + "RERANK:".Length).Trim()
        Else
            item.Question = text
            item.RerankQuery = text
        End If
        Return item
    End Function

    ' Extracts the Findit6 result-list section beginning at its marker.
    ' An empty string is returned when the marker is missing.
    Public Function ExtractResultList(fileContent As String) As String
        Dim resultMarker As String = ">>>>>>>>>>>>"
        Dim markerIndex As Integer = fileContent.IndexOf(resultMarker)
        If markerIndex < 0 Then
            Return ""
        End If
        ' Extract everything from marker onwards
        Dim resultList As String = fileContent.Substring(markerIndex)
        Return resultList.Trim()
    End Function

#End Region

#Region "Reranking"
    ' Reranking limits expensive model reads when many documents contain similar information.
    ' A separate reranker scores each Findit6 hit against the current detail question.
    ' Only the highest-value documents are forwarded to the AI for detailed reading.
    ' The surrounding code manages batching, service lifetime and duplicate suppression.

    ' Starts the reranker service when necessary and reports its startup state.
    ' Repeated calls reuse Ready, Starting and Failed states instead of launching duplicate services.
    Public Sub StartRerankWorker()
        SyncLock m_RerankerStateLock
            Select Case m_RerankerStartupState
                Case RerankerStartupState.Ready
                    CurrentAction = ChunkActionEnum.Rerank
                Case RerankerStartupState.Failed
                Case RerankerStartupState.Starting
                Case RerankerStartupState.NotStarted
                    m_RerankerStartupState = RerankerStartupState.Starting
                    m_RerankerStartupError = ""
                    Task.Run(
                    Sub()
                        EnsureRerankerServiceRunningInBackground()
                    End Sub)
            End Select
        End SyncLock
    End Sub

    ' Starts the local Python reranker service and waits for readiness.
    ' Existing healthy services are reused and startup failures are published through shared state.
    Private Sub EnsureRerankerServiceRunningInBackground()

        Const healthUrl As String = "http://127.0.0.1:8766/health"

        Try

            If IsRerankerServiceReady(healthUrl) Then

                If m_RerankerProcess Is Nothing Then
                    LogDebug("Reranker service already running, but m_RerankerProcess is Nothing.")
                Else
                    LogDebug("Reranker service already running. Stored PID=" & m_RerankerProcess.Id.ToString() & ", HasExited=" & m_RerankerProcess.HasExited.ToString())
                End If

                SetRerankerStartupReady()
                Return

            End If

            Dim rerankerDir As String = Path.Combine(Application.StartupPath, "Reranker")
            Dim pythonExe As String = Path.Combine(rerankerDir, "Python", "python.exe")
            Dim serviceFile As String = Path.Combine(rerankerDir, "reranker_service_bge.py")

            If Not Directory.Exists(rerankerDir) Then
                SetRerankerStartupFailed("Reranker directory not found: " & rerankerDir)
                Return
            End If

            If Not File.Exists(pythonExe) Then
                SetRerankerStartupFailed("Reranker Python not found: " & pythonExe)
                Return
            End If

            If Not File.Exists(serviceFile) Then
                SetRerankerStartupFailed("Reranker service not found: " & serviceFile)
                Return
            End If

            LogDebug("Starting BGE reranker service with: " & pythonExe)

            Dim psi As New ProcessStartInfo With {
            .FileName = pythonExe,
            .Arguments = "reranker_service_bge.py",
            .WorkingDirectory = rerankerDir,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .WindowStyle = ProcessWindowStyle.Hidden,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True
        }

            psi.Environment("PYTHONUTF8") = "1"
            psi.Environment("HF_HUB_OFFLINE") = "1"
            psi.Environment("TRANSFORMERS_OFFLINE") = "1"

            Dim process As New Process With {
            .StartInfo = psi,
            .EnableRaisingEvents = True
        }

            AddHandler process.OutputDataReceived,
            Sub(sender As Object, e As DataReceivedEventArgs)
                If e.Data IsNot Nothing Then
                    LogDebug("[RERANKER] " & e.Data)
                End If
            End Sub

            AddHandler process.ErrorDataReceived,
            Sub(sender As Object, e As DataReceivedEventArgs)
                If e.Data IsNot Nothing Then
                    LogDebug("RERANKER: " & e.Data)
                End If
            End Sub

            If Not process.Start() Then
                process.Dispose()
                SetRerankerStartupFailed("Process.Start returned False.")
                Return
            End If

            m_RerankerProcess = process

            process.BeginOutputReadLine()
            process.BeginErrorReadLine()

            LogDebug("Reranker process started. PID=" & process.Id.ToString())

            Dim startTime As DateTime = DateTime.UtcNow
            Const maxWaitSeconds As Integer = 180

            Do

                Threading.Thread.Sleep(2000)

                If process.HasExited Then
                    SetRerankerStartupFailed(
                    "Reranker process exited during startup. ExitCode=" &
                    process.ExitCode.ToString()
                )
                    Return
                End If

                If IsRerankerServiceReady(healthUrl) Then
                    LogDebug("Reranker service is ready.")
                    SetRerankerStartupReady()
                    Return
                End If

                If DateTime.UtcNow.Subtract(startTime).TotalSeconds >= maxWaitSeconds Then
                    SetRerankerStartupFailed("Reranker service startup timeout.")
                    Return
                End If

            Loop

        Catch ex As Exception

            SetRerankerStartupFailed(
            "EnsureRerankerServiceRunningInBackground: " & ex.Message
        )

        End Try

    End Sub

    ' Checks the reranker health endpoint with a short timeout.
    ' Connection failures are treated as a simple not-ready result.
    Private Function IsRerankerServiceReady(healthUrl As String) As Boolean
        Try
            Using client As New HttpClient()
                client.Timeout = TimeSpan.FromSeconds(3)
                Using response As HttpResponseMessage = client.GetAsync(healthUrl).Result
                    Return response.IsSuccessStatusCode
                End Using
            End Using
        Catch
            Return False
        End Try
    End Function

    ' Publishes a successful reranker startup state.
    ' The shared startup error is cleared under the reranker-state lock.
    Private Sub SetRerankerStartupReady()
        SyncLock m_RerankerStateLock
            m_RerankerStartupState = RerankerStartupState.Ready
            m_RerankerStartupError = ""
        End SyncLock
    End Sub

    ' Publishes a failed reranker startup state and cleans up a process started by this application.
    ' Workflow state remains controlled by ChunkModeRunner.
    Private Sub SetRerankerStartupFailed(errorMessage As String)
        LogDebug("[ERROR] " & errorMessage)

        Dim processToDispose As Process = m_RerankerProcess
        If processToDispose IsNot Nothing Then
            Try
                If Not processToDispose.HasExited Then
                    LogDebug("Stopping failed reranker process. PID=" & processToDispose.Id.ToString())
                    processToDispose.Kill(entireProcessTree:=True)
                    processToDispose.WaitForExit(5000)
                End If
            Catch ex As Exception
                LogDebug("[ERROR] Failed reranker cleanup: " & ex.Message)
            Finally
                Try
                    processToDispose.Dispose()
                Catch
                End Try
                If Object.ReferenceEquals(m_RerankerProcess, processToDispose) Then m_RerankerProcess = Nothing
            End Try
        End If

        SyncLock m_RerankerStateLock
            m_RerankerStartupState = RerankerStartupState.Failed
            m_RerankerStartupError = errorMessage
        End SyncLock
    End Sub

    ' Extracts existing source files from the Findit6 result list.
    ' The completed work list is handed to the first rerank batch.
    Private Sub BuildRerankFileList(ByVal sFinditResultfile As String)
        System.Threading.Thread.Sleep(100)

        If Not IO.File.Exists(sFinditResultfile) Then
            LogDebug("[ERROR] BuildRerankFileList: Findit result file not found.")
            CurrentAction = ChunkActionEnum.FinishedALL
            Exit Sub
        End If
        Dim fileContent As String = IO.File.ReadAllText(sFinditResultfile)
        Dim resultList As String = ExtractResultList(fileContent)

        If String.IsNullOrWhiteSpace(resultList) Then
            LogDebug("[ERROR] BuildRerankFileList: Findit result list is empty.")
            CurrentAction = ChunkActionEnum.FinishedALL
            Exit Sub
        End If
        Dim bFirstLine As Boolean = True

        For Each sLine As String In resultList.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            If bFirstLine Then
                bFirstLine = False
                Continue For
            End If
            Dim sFile As String = sLine.Trim()
            Dim iArrow As Integer = sFile.IndexOf("→"c)
            If iArrow >= 0 Then sFile = sFile.Substring(0, iArrow).Trim()
            sFile = sFile.Trim(""""c).Trim()
            If sFile <> "" AndAlso IO.File.Exists(sFile) AndAlso Not query.RerankList.Contains(sFile) Then query.RerankList.Add(sFile)
        Next

        If query.RerankList.Count = 0 Then
            LogDebug("[ERROR] BuildRerankFileList: no usable source files found.")
            CurrentAction = ChunkActionEnum.FinishedALL
            Exit Sub
        End If

        CurrentAction = ChunkActionEnum.RerankBATCH
        ProcessNextRerankBatch()
    End Sub

    ' Builds and submits the next character-limited rerank batch.
    ' Question and file counters advance until every detail question has been processed.
    Private Sub ProcessNextRerankBatch()
        Const maxRequestChars As Integer = 20000

        Try
            If query.RerankList Is Nothing OrElse query.RerankList.Count = 0 Then Exit Sub
            If query.DetailQuestions Is Nothing OrElse query.DetailQuestions.Count = 0 Then Exit Sub
            If query.CurrentRerankNr < 0 OrElse query.CurrentRerankNr >= query.DetailQuestions.Count Then Exit Sub

            Dim item As DetailQuestionItem = query.DetailQuestions(query.CurrentRerankNr)
            Dim displayQuestion As String = item.Question
            Dim rerankQuery As String = item.RerankQuery

            If String.IsNullOrWhiteSpace(displayQuestion) Then Exit Sub
            If String.IsNullOrWhiteSpace(rerankQuery) Then rerankQuery = displayQuestion

            If query.RerankCounter = 0 Then
                LogDebug("---> Reranking question " & (query.CurrentRerankNr + 1).ToString() & " of " & query.DetailQuestions.Count.ToString() & ": " & displayQuestion)
                LogDebug("---> Rerank query: " & rerankQuery)
                query.RerankStart = DateTime.Now

                If query.RerankResults.ContainsKey(displayQuestion) Then
                    query.RerankResults(displayQuestion).Clear()
                Else
                    query.RerankResults.Add(displayQuestion, New List(Of RerankHit))
                End If
            End If

            Dim batchDocuments As New List(Of String)
            Dim batchFiles As New List(Of String)
            Dim batchChars As Integer = 0

            Do While query.RerankCounter < query.RerankList.Count
                ' Collect documents until the current batch is full or the file list is exhausted.
                ' Stop adding documents once the 20,000-character target is reached.
                Dim orgFile As String = query.RerankList(query.RerankCounter)
                Dim candidateText As String = BuildRerankCandidateText(orgFile)
                If String.IsNullOrWhiteSpace(candidateText) Then
                    query.RerankCounter += 1
                    Continue Do
                End If
                If batchDocuments.Count > 0 AndAlso batchChars + candidateText.Length > maxRequestChars Then Exit Do
                batchDocuments.Add(candidateText)
                batchFiles.Add(orgFile)
                batchChars += candidateText.Length
                query.RerankCounter += 1
                If batchChars >= maxRequestChars Then Exit Do
            Loop

            If batchDocuments.Count = 0 Then
                If query.RerankCounter >= query.RerankList.Count Then
                    query.CurrentRerankNr += 1
                    query.RerankCounter = 0
                    If query.CurrentRerankNr < query.DetailQuestions.Count Then
                        CurrentAction = ChunkActionEnum.RerankBATCH
                        ProcessNextRerankBatch()
                        Exit Sub
                    End If
                    CurrentAction = ChunkActionEnum.RerankCleanUp
                    Exit Sub
                End If
            End If

            NewLineForRTF = "Relevance-ranked file No " & query.RerankCounter.ToString() & " of " & query.RerankList.Count.ToString() & " found files"
            If query.DetailQuestions.Count > 1 Then NewLineForRTF &= " - Question No " & (query.CurrentRerankNr + 1).ToString() & " of " & query.DetailQuestions.Count.ToString()
            If query.RerankList.Count / query.RerankCounter < 10 Then NewLineForRTF &= GetRerankRemainingTime(query.RerankCounter, query.RerankList.Count)

            LogDebug("---> Sending rerank batch: " & batchFiles.Count.ToString() & " files, " & batchChars.ToString() & " chars")
            CurrentAction = ChunkActionEnum.RerankWORKING

            Task.Run(
            Sub()
                SendRerankBatch(displayQuestion, rerankQuery, batchDocuments, batchFiles)
                If query.RerankCounter >= query.RerankList.Count Then
                    query.RerankResults(displayQuestion) = query.RerankResults(displayQuestion).OrderByDescending(Function(hit) hit.Score).ToList()
                    LogDebug("Question '" & displayQuestion & "' positive hits: " & query.RerankResults(displayQuestion).Count.ToString())
                    query.CurrentRerankNr += 1
                    If query.CurrentRerankNr >= query.DetailQuestions.Count Then
                        CurrentAction = ChunkActionEnum.RerankCleanUp
                        Exit Sub
                    End If
                    query.RerankCounter = 0
                End If
                CurrentAction = ChunkActionEnum.RerankBATCH
                ProcessNextRerankBatch()
            End Sub)

        Catch ex As Exception
            LogDebug("[ERROR] ProcessNextRerankBatch: " & ex.Message)
            CurrentAction = ChunkActionEnum.FinishedALL
        End Try
    End Sub

    ' Builds one reranker document from the cached UTF-8 source text.
    ' Original path and last-modified metadata are prepended to the document content.
    Private Function BuildRerankCandidateText(ByVal orgFile As String) As String
        Try
            Dim utf8Filename As String = HashPathName(orgFile)
            Dim utf8filePath As String = Path.Combine(sUTFpath, utf8Filename) & ".txt"
            If Not File.Exists(utf8filePath) Then
                Return ""
            End If
            Dim fileContent As String = File.ReadAllText(utf8filePath, System.Text.Encoding.UTF8)
            Dim lastModified As String = ""
            If File.Exists(orgFile) Then
                lastModified = File.GetLastWriteTime(orgFile).ToString("yyyy-MM-dd HH:mm:ss")
            End If
            Return "File: " & orgFile & vbCrLf &
               "LastModified: " & lastModified & vbCrLf & vbCrLf & fileContent
        Catch ex As Exception
            LogDebug("[ERROR] BuildRerankCandidateText: " & orgFile & " / " & ex.Message)
            Return ""
        End Try
    End Function

    ' Sends one document batch to the local reranker HTTP service.
    ' Returned scores are mapped back to the aligned source-file list.
    Private Sub SendRerankBatch(ByVal displayQuestion As String, ByVal rerankQuery As String, ByVal batchDocuments As List(Of String), ByVal batchFiles As List(Of String))
        If batchDocuments Is Nothing OrElse batchDocuments.Count = 0 Then Exit Sub
        If batchFiles Is Nothing OrElse batchFiles.Count <> batchDocuments.Count Then
            LogDebug("[ERROR] SendRerankBatch: batchFiles mismatch")
            Exit Sub
        End If
        If String.IsNullOrWhiteSpace(displayQuestion) Then
            LogDebug("[ERROR] SendRerankBatch: displayQuestion is empty")
            Exit Sub
        End If
        If String.IsNullOrWhiteSpace(rerankQuery) Then
            rerankQuery = displayQuestion
        End If
        Try
            Dim payload As New RerankRequest With {.query = rerankQuery, .documents = batchDocuments}
            Dim jsonOptions As New JsonSerializerOptions With {.PropertyNamingPolicy = JsonNamingPolicy.CamelCase}
            Dim jsonBody As String = JsonSerializer.Serialize(payload, jsonOptions)
            Using client As New HttpClient()
                client.Timeout = TimeSpan.FromMinutes(30)
                Using content As New StringContent(jsonBody, Encoding.UTF8, "application/json")
                    Dim response As HttpResponseMessage = client.PostAsync("http://127.0.0.1:8766/rerank", content).Result
                    Dim responseText As String = response.Content.ReadAsStringAsync().Result
                    If Not response.IsSuccessStatusCode Then
                        LogDebug("[ERROR] Reranker HTTP " & CInt(response.StatusCode).ToString() & ": " & responseText)
                        LogDebug("[WARN] Rerank batch skipped. Processing will continue with the remaining files.")
                        Exit Sub
                    End If
                    Dim rerankResponse As RerankResponse = JsonSerializer.Deserialize(Of RerankResponse)(responseText, jsonOptions)
                    If rerankResponse Is Nothing OrElse rerankResponse.results Is Nothing Then
                        LogDebug("[ERROR] SendRerankBatch: invalid response")
                        LogDebug("[WARN] Rerank batch skipped. Processing will continue with the remaining files.")
                        Exit Sub
                    End If
                    LogDebug("<--- RerankBatch received: " & rerankResponse.results.Count.ToString() & " scores")
                    ' Map returned scores back to their source files.
                    For Each r As RerankResult In rerankResponse.results
                        If r.index >= 0 AndAlso r.index < batchFiles.Count Then
                            Dim orgFile As String = batchFiles(r.index)
                            If Not query.RerankResults.ContainsKey(displayQuestion) Then
                                query.RerankResults.Add(displayQuestion, New List(Of RerankHit))
                            End If
                            query.RerankResults(displayQuestion).Add(New RerankHit With {.FilePath = orgFile, .LastModified = File.GetLastWriteTime(orgFile), .Score = r.score})
                        Else
                            LogDebug("[WARN] Reranker returned invalid index: " &
                                 r.index.ToString())
                        End If
                    Next
                End Using
            End Using
        Catch ex As Exception
            LogDebug("[ERROR] SendRerankBatch: " & ex.Message)
            LogDebug("[WARN] Rerank batch skipped. Processing will continue with the remaining files.")
        End Try
    End Sub

    ' Runs duplicate detection independently for every reranker question.
    ' Hit lists with fewer than two entries are skipped.
    Private Sub MarkProbableDuplicatesInRerankList()
        If query.RerankResults Is Nothing OrElse query.RerankResults.Count = 0 Then Exit Sub
        For Each rerankEntry As KeyValuePair(Of String, List(Of RerankHit)) In query.RerankResults
            Dim rerankQuestion As String = rerankEntry.Key
            Dim rerankHits As List(Of RerankHit) = rerankEntry.Value
            If rerankHits Is Nothing OrElse rerankHits.Count < 2 Then Continue For
            LogDebug("Starting duplicate check for " & rerankHits.Count.ToString() & " rerank hits: " & rerankQuestion)
            MarkProbableDuplicatesInHitList(rerankHits)
        Next
    End Sub

    ' Marks probable duplicate documents within one ranked hit list.
    ' Only accepted non-duplicates become comparison anchors for later candidates.
    Private Sub MarkProbableDuplicatesInHitList(rerankHits As List(Of RerankHit))
        For Each hit As RerankHit In rerankHits
            hit.IsProbableDuplicate = False
            hit.DuplicateOfFilePath = ""
            hit.DuplicatePercentage = 0
        Next
        Dim acceptedHits As New List(Of RerankHit)
        Dim acceptedTexts As New Dictionary(Of RerankHit, String)
        For Each candidate As RerankHit In rerankHits
            If acceptedHits.Count >= 100 Then Exit For
            Dim candidateText As String = GetTextForDuplicateCheck(candidate.FilePath)
            If String.IsNullOrWhiteSpace(candidateText) Then
                LogDebug("Duplicate check skipped because no cached UTF-8 text was found: " & candidate.FilePath)
                acceptedHits.Add(candidate)
                acceptedTexts.Add(candidate, "")
                Continue For
            End If
            Dim bestMatch As RerankHit = Nothing
            Dim bestPercentage As Double = 0
            For Each acceptedHit As RerankHit In acceptedHits
                Dim acceptedText As String = acceptedTexts(acceptedHit)
                If String.IsNullOrWhiteSpace(acceptedText) Then Continue For
                Dim currentPercentage As Double = CalculateContainedTextPercentage(candidateText, acceptedText)
                If currentPercentage > bestPercentage Then
                    bestPercentage = currentPercentage
                    bestMatch = acceptedHit
                End If
                If bestPercentage >= 99.9 Then Exit For
            Next
            If bestMatch IsNot Nothing AndAlso bestPercentage >= currentSession.DublicateThreshold Then
                candidate.IsProbableDuplicate = True
                candidate.DuplicateOfFilePath = bestMatch.FilePath
                candidate.DuplicatePercentage = bestPercentage
                'LogDebug("Probable duplicate: " & candidate.FilePath & " | " & bestPercentage.ToString("N1") & " % contained in " & bestMatch.FilePath)
            Else
                acceptedHits.Add(candidate)
                acceptedTexts.Add(candidate, candidateText)
            End If
        Next
        Dim duplicateCount As Integer = 0
        For Each hit As RerankHit In rerankHits
            If hit.IsProbableDuplicate Then duplicateCount += 1
        Next
        LogDebug("Duplicate check finished. " & duplicateCount.ToString() & " probable duplicates found.")
    End Sub

    ' Loads and normalizes the cached UTF-8 text used for duplicate comparison.
    ' Missing or unreadable cache files return an empty string.
    Private Function GetTextForDuplicateCheck(orgFilePath As String) As String
        If String.IsNullOrWhiteSpace(orgFilePath) Then Return ""
        Try
            Dim sUTFname As String = HashPathName(orgFilePath) & ".txt"
            Dim cacheTextFile As String = Path.Combine(sUTFpath, sUTFname)
            If Not File.Exists(cacheTextFile) Then Return ""
            Dim cachedText As String = File.ReadAllText(cacheTextFile, Encoding.UTF8)
            Return NormalizeTextForDuplicateComparison(cachedText)
        Catch ex As Exception
            LogDebug("Error reading cached text for duplicate check: " & orgFilePath & " | " & ex.Message)
            Return ""
        End Try
    End Function

    ' Estimates how much of the smaller document is contained in the larger one.
    ' The comparison uses sets of normalized fixed-size word blocks.
    Private Function CalculateContainedTextPercentage(firstText As String, secondText As String, Optional wordsPerBlock As Integer = 4) As Double
        If String.IsNullOrWhiteSpace(firstText) OrElse String.IsNullOrWhiteSpace(secondText) Then Return 0
        Dim firstBlocks As HashSet(Of String) = CreateWordBlocks(firstText, wordsPerBlock)
        Dim secondBlocks As HashSet(Of String) = CreateWordBlocks(secondText, wordsPerBlock)
        If firstBlocks.Count = 0 OrElse secondBlocks.Count = 0 Then Return 0
        Dim smallerBlocks As HashSet(Of String)
        Dim largerBlocks As HashSet(Of String)
        If firstBlocks.Count <= secondBlocks.Count Then
            smallerBlocks = firstBlocks
            largerBlocks = secondBlocks
        Else
            smallerBlocks = secondBlocks
            largerBlocks = firstBlocks
        End If
        Dim matchingBlocks As Integer = 0
        For Each block As String In smallerBlocks
            If largerBlocks.Contains(block) Then matchingBlocks += 1
        Next
        Return matchingBlocks / CDbl(smallerBlocks.Count) * 100.0
    End Function

    ' Normalizes text before duplicate comparison.
    ' Compatibility normalization, lowercase conversion and whitespace folding are applied.
    Private Function NormalizeTextForDuplicateComparison(text As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return ""
        text = text.Normalize(NormalizationForm.FormKC)
        text = text.ToLowerInvariant()
        text = Regex.Replace(text, "\s+", " ")
        Return text.Trim()
    End Function

    ' Creates the unique fixed-size word blocks used for duplicate comparison.
    ' Short texts become a single block containing all available words.
    Private Function CreateWordBlocks(text As String, wordsPerBlock As Integer) As HashSet(Of String)
        Dim blocks As New HashSet(Of String)(StringComparer.Ordinal)
        If String.IsNullOrWhiteSpace(text) Then Return blocks
        Dim words As String() = text.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        If words.Length < wordsPerBlock Then
            blocks.Add(String.Join(" ", words))
            Return blocks
        End If
        For i As Integer = 0 To words.Length - wordsPerBlock
            blocks.Add(String.Join(" ", words, i, wordsPerBlock))
        Next
        Return blocks
    End Function

    ' Estimates remaining rerank time after enough files have been processed.
    ' No estimate is returned during the first ten percent of the run.
    Private Function GetRerankRemainingTime(completed As Integer, total As Integer) As String
        If total <= 0 OrElse completed <= 0 Then Return ""
        If completed < Math.Ceiling(total * 0.1) Then Return ""
        Dim elapsedMinutes As Double = (DateTime.Now - query.RerankStart).TotalMinutes
        Dim minutesPerFile As Double = elapsedMinutes / completed
        Dim remainingMinutes As Double = (total - completed) * minutesPerFile
        Return " | time remaining: " & remainingMinutes.ToString("0.0") & " min"
    End Function

    ' Writes the ranked hit lists and duplicate markers to the session workspace.
    ' The text file is stored as UTF-8 without a BOM.
    Private Sub WriteRerankList()
        Try
            Dim sPath As String = Path.Combine(SessionWorkspace, "RerankList.txt")
            Dim sb As New System.Text.StringBuilder
            Dim counter As Integer = 0
            For Each kvp As KeyValuePair(Of String, List(Of RerankHit)) In query.RerankResults
                sb.AppendLine("QUESTION: " & kvp.Key)
                For Each oHit As RerankHit In kvp.Value
                    ' Use the second-highest rerank score as a robust reference.
                    ' This avoids one extreme top hit from setting the cutoff too high.
                    counter += 1
                    If counter = 2 Then query.HighestRank = oHit.Score
                    sb.Append(oHit.Score.ToString("0.0000"))
                    sb.Append(vbTab)
                    sb.Append(oHit.FilePath)
                    If oHit.IsProbableDuplicate Then
                        sb.Append(vbTab)
                        sb.Append("[SKIPPED: ")
                        sb.Append(oHit.DuplicatePercentage.ToString("0.0"))
                        sb.Append(" % text similarity with ")
                        sb.Append(oHit.DuplicateOfFilePath)
                        sb.Append("]")
                    End If
                    sb.AppendLine()
                Next
                sb.AppendLine()
            Next
            File.WriteAllText(sPath, sb.ToString(), New System.Text.UTF8Encoding(False))
        Catch ex As Exception
            LogDebug("[ERROR] WriteRerankList: " & ex.Message)
        End Try
    End Sub

    ' Requests a controlled reranker shutdown on a background task.
    ' A process-tree kill is used as fallback before the workflow advances to excerpts.
    Public Sub StopRerankerService()
        Dim processToStop As Process = m_RerankerProcess

        Task.Run(
        Sub()
            Try
                If processToStop Is Nothing Then
                    LogDebug("StopRerankerService: m_RerankerProcess is Nothing.")
                    CurrentAction = ChunkActionEnum.ExcerptsNEXT
                    Exit Sub
                End If

                LogDebug("Requesting controlled reranker shutdown. PID=" & processToStop.Id.ToString())

                Using client As New HttpClient()
                    client.Timeout = TimeSpan.FromSeconds(5)
                    Dim response As HttpResponseMessage = client.PostAsync("http://127.0.0.1:8766/shutdown", Nothing).GetAwaiter().GetResult()
                    LogDebug("Reranker shutdown response: " & response.StatusCode.ToString())
                End Using

                LogDebug("Waiting for reranker process to exit.")

                If Not processToStop.WaitForExit(30000) Then
                    LogDebug("Controlled shutdown failed after 30 seconds. Killing process tree.")
                    processToStop.Kill(entireProcessTree:=True)
                    processToStop.WaitForExit(5000)
                End If

            Catch ex As Exception
                LogDebug("Error during controlled reranker shutdown: " & ex.Message)

                Try
                    If processToStop IsNot Nothing AndAlso Not processToStop.HasExited Then
                        LogDebug("Fallback: killing reranker process tree.")
                        processToStop.Kill(entireProcessTree:=True)
                    End If
                Catch killEx As Exception
                    LogDebug("Fallback kill failed: " & killEx.Message)
                End Try

            Finally
                If processToStop IsNot Nothing Then
                    Try
                        processToStop.Dispose()
                    Catch
                    End Try
                End If

                If Object.ReferenceEquals(m_RerankerProcess, processToStop) Then m_RerankerProcess = Nothing
                LogDebug("Reranker service stopped and cleaned up.")
                CurrentAction = ChunkActionEnum.ExcerptsNEXT
            End Try
        End Sub)
    End Sub


#End Region

#Region "Excerpts"
    ' Read the highest-ranked non-duplicate files and collect relevant excerpts.

    ' Anthropic uses a fresh session for each detail question.
    ' This preserves the available session context for reading its source files.
    ' Initializes the excerpt chain and starts processing the first rerank hit.
    ' The indices are reset only once when a new excerpt chain begins.
    Private Sub StartExcerptChain()
        Try
            If currentSession Is Nothing Then
                LogDebug("[ERROR] StartExcerptChain: currentSession is Nothing")
                CurrentAction = ChunkActionEnum.FinishedALL
                Exit Sub
            End If

            query.ExcerptHitIndex = 0

            If currentSession.Provider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase) Then
                CurrentAction = ChunkActionEnum.SessionRestart
                RestartAnthropicSessionAndStartExcerptChain()
            Else
                RunNextExcerptFromRerankResults()
            End If
        Catch ex As Exception
            LogDebug("[ERROR] StartExcerptChain: " & ex.Message)
            CurrentAction = ChunkActionEnum.FinishedALL
        End Try
    End Sub

    ' Restarts the Anthropic session before excerpt processing begins.
    ' Processing continues with the first available rerank hit afterwards.
    Private Async Sub RestartAnthropicSessionAndStartExcerptChain()
        Try
            Await frmMain.RestartAnthropicSessionAsync()
            query.ExcerptHitIndex = 0
            RunNextExcerptFromRerankResults()
        Catch ex As Exception
            LogDebug("[ERROR] RestartAnthropicSessionAndStartExcerptChain: " & ex.Message)
            CurrentAction = ChunkActionEnum.FinishedALL
        End Try
    End Sub

    ' Returns the next usable rerank hit and its associated detail question.
    ' Empty entries and probable duplicates are skipped automatically.
    Private Function GetNextExcerptHit(ByRef question As String, ByRef hit As RerankHit) As Boolean

        question = ""
        hit = Nothing

        If currentSession Is Nothing Then Return False
        If query.RerankResults Is Nothing OrElse query.RerankResults.Count = 0 Then Return False
        If query.ExcerptQuestionIndex < 0 OrElse query.ExcerptQuestionIndex >= query.DetailQuestions.Count Then Return False

        question = query.DetailQuestions(query.ExcerptQuestionIndex).Question

        If Not query.RerankResults.ContainsKey(question) Then Return False

        Dim hits As List(Of RerankHit) = query.RerankResults(question)
        If hits Is Nothing Then Return False

        Do While query.ExcerptHitIndex < hits.Count

            hit = hits(query.ExcerptHitIndex)

            ' This hit is now consumed.
            query.ExcerptHitIndex += 1

            If hit Is Nothing Then Continue Do

            If hit.IsProbableDuplicate Then
                LogDebug("Skipping probable duplicate for excerpt creation: " &
                     hit.FilePath & " | " &
                     hit.DuplicatePercentage.ToString("0.0") & " % of " &
                     hit.DuplicateOfFilePath)
                hit = Nothing
                Continue Do
            End If

            Return True
        Loop

        hit = Nothing
        Return False

    End Function

    ' Selects the next rerank hit and starts its excerpt prompt.
    ' The output file is assigned only after the actual detail question is known.
    Public Sub RunNextExcerptFromRerankResults()
        Try
            Dim question As String = ""
            Dim hit As RerankHit = Nothing

            If Not GetNextExcerptHit(question, hit) Then
                CurrentAction = ChunkActionEnum.SynthesisSTART
                LogDebug("RunNextExcerptFromRerankResults: no more hits.")
                Exit Sub
            End If

            If String.IsNullOrWhiteSpace(question) Then
                LogDebug("[ERROR] RunNextExcerptFromRerankResults: question is empty")
                Exit Sub
            End If

            If hit Is Nothing OrElse String.IsNullOrWhiteSpace(hit.FilePath) Then
                LogDebug("[ERROR] RunNextExcerptFromRerankResults: hit or filepath is empty")
                CurrentAction = ChunkActionEnum.ExcerptsNEXT
                Exit Sub
            End If

            Dim questionNumber As Integer = query.ExcerptQuestionIndex + 1
            If questionNumber <= 0 Then questionNumber = 1

            query.ExcerptOutputFile = Path.Combine(SessionWorkspace, "Excerpts_" & questionNumber.ToString() & ".txt")
            query.ExcerptFile = hit.FilePath

            SendExcerptPrompt(question, hit)
        Catch ex As Exception
            LogDebug("[ERROR] RunNextExcerptFromRerankResults: " & ex.Message)
            CurrentAction = ChunkActionEnum.ExcerptsNEXT
        End Try
    End Sub

    ' Builds and sends an excerpt prompt for one reranked source file.
    ' Prompt size and template selection depend on the cached source text.
    Private Sub SendExcerptPrompt(ByVal question As String, ByVal hit As RerankHit)
        Try
            If currentSession Is Nothing Then Exit Sub
            If hit Is Nothing OrElse String.IsNullOrWhiteSpace(hit.FilePath) Then Exit Sub

            Dim sUTFname As String = HashPathName(hit.FilePath) & ".txt"
            Dim cacheTextFile As String = Path.Combine(sUTFpath, sUTFname)

            If Not File.Exists(cacheTextFile) Then
                LogDebug("[WARN] AgentCache text not found for excerpt: " & cacheTextFile)
                CurrentAction = ChunkActionEnum.ExcerptsNEXT
                Exit Sub
            End If

            Dim fileText As String = File.ReadAllText(cacheTextFile, Encoding.UTF8)

            If String.IsNullOrWhiteSpace(fileText) Then
                LogDebug("[WARN] Empty excerpt source: " & hit.FilePath)
                CurrentAction = ChunkActionEnum.ExcerptsNEXT
                Exit Sub
            End If

            Dim mdPath As String
            If fileText.Length < 5000 Then
                mdPath = Path.Combine(AgentWorkspace, "chunk_06_ExcerptShort.md")
                query.ExcerptText = fileText
            ElseIf fileText.Length < 10000 Then
                mdPath = Path.Combine(AgentWorkspace, "chunk_06_ExcerptMed.md")
                query.ExcerptText = fileText
            Else
                mdPath = Path.Combine(AgentWorkspace, "chunk_06_ExcerptLong.md")
            End If

            If Not File.Exists(mdPath) Then
                LogDebug("[ERROR] Excerpt markdown not found: " & mdPath)
                CurrentAction = ChunkActionEnum.ExcerptsNEXT
                Exit Sub
            End If

            Dim prompt As String = LoadChunkMarkdown(mdPath)
            prompt = prompt.Replace("%Teilfrage%", question)
            prompt = prompt.Replace("%Textlänge%", fileText.Length.ToString())
            prompt = prompt.Replace("%Dateiinhalt%", fileText)

            If CurrentAction <> ChunkActionEnum.SynthesisSTART Then CurrentAction = ChunkActionEnum.ExcerptsRUN

            LogDebug("----------->SendExcerptPrompt for " & hit.FilePath & " - " & cacheTextFile)
            LogDebug("Ranking was: " & hit.Score.ToString() & " Length of text: " & fileText.Length.ToString())

            query.ExcerptWholeSendedSize += fileText.Length
            query.ExcerptPromptStarted = DateTime.Now

            frmMain.SendPromptAsync(prompt, False, True, useGuidedSettings:=True)
        Catch ex As Exception
            LogDebug("[ERROR] SendExcerptPrompt: " & ex.Message)
            CurrentAction = ChunkActionEnum.ExcerptsNEXT
        End Try
    End Sub

    ' Processes one completed excerpt response and decides whether reading should continue.
    ' Relevance, ranking, count and size limits can all advance the workflow to synthesis.
    ' Stores the returned excerpt, checks all stop conditions and continues with the next ranked file.
    ' The next excerpt is started directly; StartExcerptChain must only be entered once per excerpt chain.
    Sub ExcerptReturned(jResponse As JsonElement)
        Try
            ' Store the returned excerpt or relevance result.
            Dim msg As String = ExcerptStorage(jResponse)

            ' Update the progress line shown to the user.
            NewLineForRTF = "Agent read file No " & query.ExcerptHitIndex.ToString &
            " - Question No " & (query.ExcerptQuestionIndex + 1).ToString &
            " with " & msg & " | Excerpt sum now: " & query.excerptSize.ToString

            ' External sessions must also respect practical context limits.
            If currentSession.Provider.Trim <> "Ollama" Then
                If query.ExcerptWholeSendedSize > 500000 Then CurrentAction = ChunkActionEnum.SynthesisSTART
                If query.excerptSize > currentSession.MaxExcerptLength Then CurrentAction = ChunkActionEnum.SynthesisSTART
            End If

            ' Several consecutive irrelevant excerpts indicate that useful rerank hits are exhausted.
            If msg.Contains("no relevance") Then
                query.irrelevantExcerptCount += 1
            Else
                query.irrelevantExcerptCount = 0
            End If
            If query.irrelevantExcerptCount = 4 Then CurrentAction = ChunkActionEnum.SynthesisSTART

            ' Low ranking values after several excerpts also trigger synthesis.
            Dim displayQuestion As String = query.DetailQuestions(query.ExcerptQuestionIndex).Question
            Dim hits As List(Of RerankHit) = query.RerankResults(displayQuestion)
            Dim nextHitIndex As Integer = query.ExcerptHitIndex

            If query.ExcerptHitIndex > 5 AndAlso nextHitIndex < hits.Count AndAlso hits(nextHitIndex).Score < query.HighestRank - 5 Then
                CurrentAction = ChunkActionEnum.SynthesisSTART
                LogDebug(">>>> Ranking getting too low -> FinalSynthesis")
            ElseIf nextHitIndex >= hits.Count Then
                CurrentAction = ChunkActionEnum.SynthesisSTART
                LogDebug(">>>> All rerank hits excerpted -> FinalSynthesis")
            End If

            ' Apply an absolute safety limit to the number of excerpt attempts.
            If query.ExcerptHitIndex > 21 Then CurrentAction = ChunkActionEnum.SynthesisSTART

            ' Stop once the configured excerpt length limit is reached.
            If ExcerptLengthLimitReached() Then
                CurrentAction = ChunkActionEnum.SynthesisSTART
                LogDebug(">>>> Excerpt max length reached -> FinalSynthesis")
            End If

            ' Continue directly with the next ranked source while the excerpt chain is active.
            If CurrentAction <> ChunkActionEnum.SynthesisSTART Then
                CurrentAction = ChunkActionEnum.ExcerptsNEXT
                RunNextExcerptFromRerankResults()
            End If

        Catch ex As Exception
            LogDebug("[ERROR] ExcerptReturned: " & ex.Message)
        End Try
    End Sub

    ' Interprets and stores the Answer returned by an excerpt request.
    ' Local Ollama runs append the resulting evidence to the physical excerpt file.
    Public Function ExcerptStorage(ByVal jResponse As JsonElement) As String
        Try
            Dim excerptText As String = ""
            Dim prop As JsonElement
            Dim StatString As String = ""

            If jResponse.TryGetProperty("Answer", prop) Then
                excerptText = If(prop.GetString(), "")
                If Not String.IsNullOrEmpty(excerptText) Then
                    excerptText = excerptText.Replace("\\n", vbCrLf).Replace("\n", vbCrLf)
                End If
            Else
                excerptText = "no valid json"
            End If

            If excerptText.Trim.Equals("RELEVANT", StringComparison.OrdinalIgnoreCase) Then
                excerptText = query.ExcerptText
                query.excerptSize += excerptText.Length
                StatString = "mostly relevant parts"

            ElseIf excerptText.Trim.Equals("NO_RELEVANCE", StringComparison.OrdinalIgnoreCase) Then
                StatString = "no relevance. "

            Else
                query.excerptSize += excerptText.Length
                StatString = excerptText.Length.ToString() & " relevant chars. "
            End If

            excerptText =
            "Filename: " & query.ExcerptFile & vbCrLf &
            "Filedate: " & IO.File.GetLastWriteTime(query.ExcerptFile).ToShortDateString() &
            vbCrLf & vbCrLf & excerptText

            Dim separator As String =
            vbCrLf & vbCrLf &
            "====================================================================" & vbCrLf &
            "END OF FILE EXCERPT No: " & query.ExcerptHitIndex.ToString &
            " OrgLength: " & excerptText.Length.ToString &
            " > " & StatString & vbCrLf &
            "====================================================================" & vbCrLf & vbCrLf

            File.AppendAllText(query.ExcerptOutputFile, excerptText & separator, System.Text.Encoding.UTF8)

            LogDebug(">>>> Excerpt received and written: " & StatString &
                 " | " & query.ExcerptOutputFile)

            Return StatString

        Catch ex As Exception
            LogDebug("[ERROR] ExcerptReceived: " & ex.Message)
            Return "[ERROR] ExcerptReceived: " & ex.Message
        End Try
    End Function

    ' Checks whether the local excerpt file has reached its configured size limit.
    ' Read failures fail safe by reporting that the limit has been reached.
    Public Function ExcerptLengthLimitReached() As Boolean
        If currentSession Is Nothing Then Return True
        If currentSession.Provider.Trim <> "Ollama" Then Return False
        If String.IsNullOrWhiteSpace(query.ExcerptOutputFile) Then Return False
        If Not IO.File.Exists(query.ExcerptOutputFile) Then Return False
        ' Only local-model synthesis depends on this physical excerpt file.
        Try
            Dim excerptLength As Long = New IO.FileInfo(query.ExcerptOutputFile).Length
            LogDebug("Excerpt length: " & excerptLength.ToString() & " / " & currentSession.MaxExcerptLength.ToString())
            Dim bLimit As Boolean = excerptLength >= currentSession.MaxExcerptLength
            Return bLimit
        Catch ex As Exception
            LogDebug("[ERROR] ExcerptLengthLimitReached: " & ex.Message)
            Return True
        End Try
    End Function

#End Region

#Region "Synthesis and Output"

    ' Builds and sends the synthesis prompt for the current detail question.
    ' Local models receive the excerpt file explicitly; external models use their retained session context.
    Public Function StartSynthesisForCurrentExcerptFile() As String
        Const userError As String = "Synthesis could not be started."

        Try
            If currentSession Is Nothing Then
                LogDebug("[ERROR] StartSynthesisForCurrentExcerptFile: currentSession is Nothing")
                Return userError
            End If

            Dim excerpts As String
            Dim mdPath As String

            If currentSession.Provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase) OrElse
                  currentSession.Provider.Equals("Perplexity", StringComparison.OrdinalIgnoreCase) OrElse
                   TypeOf currentProvider Is OpenAICompatibleProvider Then

                ' Chunked models receive the accumulated excerpt text explicitly.
                If String.IsNullOrWhiteSpace(query.ExcerptOutputFile) Then
                    LogDebug("[ERROR] StartSynthesisForCurrentExcerptFile: ExcerptOutputFile is empty")
                    Return userError
                End If

                If Not File.Exists(query.ExcerptOutputFile) Then
                    LogDebug("[ERROR] StartSynthesisForCurrentExcerptFile: ExcerptOutputFile not found: " & query.ExcerptOutputFile)
                    Return userError
                End If

                excerpts = File.ReadAllText(query.ExcerptOutputFile, Encoding.UTF8)

                If String.IsNullOrWhiteSpace(excerpts) Then
                    LogDebug("[ERROR] StartSynthesisForCurrentExcerptFile: excerpts file is empty: " & query.ExcerptOutputFile)
                    Return userError
                End If

                mdPath = Path.Combine(AgentWorkspace, "Chunk_8_Synthesis.md")

            Else
                ' Stateful BigModel providers use the retained session context.
                mdPath = Path.Combine(AgentWorkspace, "Chunk_8_SynthesisBigMod.md")
                excerpts = ""
            End If

            If Not File.Exists(mdPath) Then
                LogDebug("[ERROR] StartSynthesisForCurrentExcerptFile: markdown not found: " & mdPath)
                Return userError
            End If

            Dim prompt As String = LoadChunkMarkdown(mdPath)
            prompt = prompt.Replace("%Userfrage%", query.UserQuestion)
            m_CurrentSynthesisQuestion = query.DetailQuestions(query.ExcerptQuestionIndex).Question
            prompt = prompt.Replace("%Teilfrage%", query.DetailQuestions(query.ExcerptQuestionIndex).Question)
            prompt = prompt.Replace("%Query%", query.AIquestion)
            prompt = prompt.Replace("%Excerpts%", excerpts)

            LogDebug(">>>> StartSynthesisForCurrentExcerptFile")
            frmMain.SendPromptAsync(prompt, False, True, useGuidedSettings:=True)
            Return ""

        Catch ex As Exception
            LogDebug("[ERROR] StartSynthesisForCurrentExcerptFile: " & ex.Message)
            Return userError
        End Try
    End Function

    ' Displays a completed synthesis and advances to the next detail question when needed.
    ' After the last local question the complete Chunk Mode run is marked finished.
    Public Function ShowSynthesis(jResponse As JsonElement) As String
        Dim SMsgToUser As String = ""
        frmMain.Spinner.Visible = False
        LogDebug(">>>> SynthesisReady")
        Try
            Dim prop As JsonElement
            Dim synthesisText As String = ""
            If jResponse.TryGetProperty("Answer", prop) Then
                synthesisText = prop.GetString()
                If Not String.IsNullOrEmpty(synthesisText) Then synthesisText = synthesisText.Replace("\\n", vbCrLf).Replace("\n", vbCrLf)
            End If
            If String.IsNullOrWhiteSpace(synthesisText) Then
                SMsgToUser = "Synthesis completed, but text is missing."
            Else
                synthesisText = RemoveDuplicateParagraphs(synthesisText)
                AppendResultTextWithFileLinks(synthesisText & vbCrLf & vbCrLf)
            End If
            LogDebug(">>>> synthesisReady CASE EXIT")
        Catch ex As Exception
            LogDebug("[ERROR] ShowSynthesis: " & ex.Message)
            CurrentAction = ChunkActionEnum.FinishedALL
            Return "Synthesis result could not be processed."
        End Try
        ' Local Chunk Mode continues with the next detail question if necessary.
        Dim questions As List(Of String) = query.RerankResults.Keys.ToList()
        query.ExcerptQuestionIndex += 1
        If query.ExcerptQuestionIndex < query.DetailQuestions.Count Then
            LogDebug(">>>> Starting next excerpt/synthesis round: Question " &
             (query.ExcerptQuestionIndex + 1).ToString() & " of " &
             query.DetailQuestions.Count.ToString())
            query.ExcerptHitIndex = 0
            query.excerptSize = 0
            query.irrelevantExcerptCount = 0
            CurrentAction = ChunkActionEnum.ExcerptsNEXT
            SMsgToUser = "Start working on answer to question " &
                 (query.ExcerptQuestionIndex + 1).ToString() & " of " &
                 query.DetailQuestions.Count.ToString() & vbCrLf
        Else
            LogDebug(">>>> All excerpt/synthesis rounds completed.")
            CurrentAction = ChunkActionEnum.FinishedALL
        End If
        Return SMsgToUser
    End Function

    ' Removes exact duplicate paragraphs from a synthesis result.
    ' Original paragraph order is retained.
    Private Function RemoveDuplicateParagraphs(ByVal synthesisText As String) As String
        If String.IsNullOrWhiteSpace(synthesisText) Then Return synthesisText
        Dim normalizedText As String = synthesisText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
        Dim paragraphs As String() = Regex.Split(normalizedText.Trim(), "\n\s*\n")
        Dim seenParagraphs As New HashSet(Of String)(StringComparer.Ordinal)
        Dim uniqueParagraphs As New List(Of String)
        For Each paragraph As String In paragraphs
            Dim cleanedParagraph As String = paragraph.Trim()
            If cleanedParagraph <> "" AndAlso seenParagraphs.Add(cleanedParagraph) Then
                uniqueParagraphs.Add(cleanedParagraph)
            End If
        Next
        Return String.Join(vbCrLf & vbCrLf, uniqueParagraphs)
    End Function

    ' Appends synthesis text to the protocol while converting file markers into links.
    ' Tolerates minor formatting errors in AI-generated file URI markers.
    Private Sub AppendResultTextWithFileLinks(ByVal text As String)
        If String.IsNullOrEmpty(text) Then Exit Sub

        Dim resultColor As Color = Color.FromArgb(170, 210, 255)
        Dim resultHeader As String = "[" & DateTime.Now.Subtract(frmMain.startTime).ToString("mm\:ss") & "] Result"

        If currentSession.blocal AndAlso Not String.IsNullOrWhiteSpace(m_CurrentSynthesisQuestion) Then
            resultHeader &= " for Question: " & m_CurrentSynthesisQuestion
        End If

        AppendPlainProtocolText(resultHeader & vbCrLf & vbCrLf, resultColor)

        text = text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Replace(vbLf, vbCrLf)

        Dim pattern As String = "\[\s*<?\s*(file:///[^\]]+?)\s*>?\s*\]"
        Dim matches As MatchCollection = Regex.Matches(text, pattern, RegexOptions.IgnoreCase)
        Dim pos As Integer = 0

        For Each m As Match In matches
            If m.Index > pos Then AppendPlainProtocolText(text.Substring(pos, m.Index - pos), resultColor)

            Dim rawUrl As String = m.Groups(1).Value.Trim()
            rawUrl = rawUrl.Replace("> ", " ").TrimEnd(">"c).Trim()

            AppendPlainProtocolText("(", resultColor)
            AppendInlineFileLinkFromUrl(rawUrl)
            AppendPlainProtocolText(")", resultColor)

            pos = m.Index + m.Length
        Next

        If pos < text.Length Then AppendPlainProtocolText(text.Substring(pos), resultColor)
    End Sub

    ' Converts a file URL into a clickable inline RTF link.
    ' Malformed URLs fall back to manual local-path decoding.
    Private Sub AppendInlineFileLinkFromUrl(ByVal fileUrl As String)
        Dim localPath As String = fileUrl
        Try
            Dim u As New Uri(fileUrl)
            localPath = u.LocalPath
        Catch
            localPath = fileUrl.Replace("file:///", "")
            localPath = Uri.UnescapeDataString(localPath)
            localPath = localPath.Replace("/", "\")
        End Try
        localPath = Uri.UnescapeDataString(localPath)
        Dim displayText As String = Path.GetFileName(localPath)
        If String.IsNullOrWhiteSpace(displayText) Then
            displayText = localPath
        End If
        Dim rtfLink As String =
        "{\rtf1\ansi " &
        "{\field{\*\fldinst HYPERLINK """ & RtfEscape(localPath) & """}" &
        "{\fldrslt " & RtfEscape(displayText) & "}}" &
        "}"
        frmMain.rtfbProtokoll.SelectionStart = frmMain.rtfbProtokoll.TextLength
        frmMain.rtfbProtokoll.SelectionLength = 0
        frmMain.rtfbProtokoll.SelectedRtf = rtfLink
    End Sub

    ' Appends plain text to the protocol at the current end position.
    ' An optional color overrides the normal application text color.
    Private Sub AppendPlainProtocolText(ByVal text As String, Optional ByVal textColor As Color? = Nothing)
        If String.IsNullOrEmpty(text) Then Exit Sub
        frmMain.rtfbProtokoll.SelectionStart = frmMain.rtfbProtokoll.TextLength
        frmMain.rtfbProtokoll.SelectionLength = 0
        If textColor.HasValue Then
            frmMain.rtfbProtokoll.SelectionColor = textColor.Value
        Else
            frmMain.rtfbProtokoll.SelectionColor = clr.Text
        End If
        frmMain.rtfbProtokoll.SelectedText = text
    End Sub

#End Region

#Region "Run Reset and AI Settings"

    ' Resets all query-specific state before a new Chunk Mode run.
    ' Provider connection state remains intact while search, reranker and accounting values are cleared.
    Public Sub ResetForNewQuery()
        query = New Sessionquery()
        ' Reranker state
        m_RerankerStartupState = RerankerStartupState.NotStarted
        m_RerankerStartupError = ""
        ' The reranker should already have been shut down cleanly.
        ' Do not kill or dispose anything here.
        If m_RerankerProcess IsNot Nothing Then
            Try
                If m_RerankerProcess.HasExited Then
                    m_RerankerProcess.Dispose()
                    m_RerankerProcess = Nothing
                Else
                    LogDebug("ResetForNewQuery: reranker process is unexpectedly still running. PID=" & m_RerankerProcess.Id.ToString())
                End If
            Catch ex As Exception
                LogDebug("ResetForNewQuery: reranker process check failed: " & ex.Message)
            End Try
        End If
        ' Findit / processing state
        bFinditRunning = False
        iFinditFoundFiles = 0
        sFinditResultfile = ""
        NewLineForRTF = ""
        ' AI communication state
        bNoThink = False
        lastStatusSentTime = DateTime.MinValue
        dtLastAgentResponse = DateTime.MinValue
        sLastAgentResponseType = ""
        dtLastPromptSent = DateTime.MinValue
        sTypeOfLastPrompt = ""
        sPromptToProcess = ""
        sPreviousPompt = ""
        KIresponse = ""
        bInvalidJson = False
        iPromptBalance = 0
        ' Current-session values that belong to one user question
        currentSession.ChunkMode = ""
        currentSession.Userlanguage = ""
        currentSession.AImood = AI_Mood.Logical
        currentSession.ActualExcpertRanking = 0
        currentSession.SearchTerm = ""
        ' Token/cost statistics are counted per user question
        currentSession.SessionStartTime = DateTime.Now
        currentSession.TokensIn = 0
        currentSession.TokensOut = 0
        currentSession.RateLimitWarningLevel = 0
        ' Run-specific paths will be assigned again for the new search
        ' Reset result-file wrapper if it belongs to the individual search
        FinditResults = New IniFile("")
        ' No worker should be running at this point.
        ' bworkerbusy is intentionally NOT forced to False here.
        CurrentAction = ChunkActionEnum.WaitForNext
        SessionWorkspace = ""
        debugLogPath = ""
    End Sub

    ' Renames the session workspace using the resolved core entity.
    ' Invalid filename characters are replaced before the directory is moved.
    Private Sub RenameSessionFolderWithCoreEntity()
        If SessionWorkspace = "" OrElse query.CoreEntity = "" Then Exit Sub
        Dim name As String = query.CoreEntity.Trim().Replace(" ", "_")
        For Each c As Char In Path.GetInvalidFileNameChars()
            name = name.Replace(c, "_"c)
        Next
        Dim newPath As String = SessionWorkspace & "_" & name
        Directory.Move(SessionWorkspace, newPath)
        SessionWorkspace = newPath
    End Sub

    ' Selects the inference profile used for the current markdown step.
    ' Mood, temperature and thinking mode are adjusted together for local-model prompts.


#End Region

End Module
