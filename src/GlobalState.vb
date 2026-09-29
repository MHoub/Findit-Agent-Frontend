' Global.bas
Imports System.IO

Public Module GlobalState
    Public Version As String = "1.0.01"
    ' where are the relevant pahts to inis and workspace?
    Public LetteringIni As New IniFile("")
    Public sPathgroupIni As String = "" ' Pfad der Pfadgruppendatei
    Public PathgroupIni As New IniFile("") ' Objekt der Pfadgruppendatei
    Public AgentsIni As New IniFile("")
    Public StandardFij As New IniFile("")
    Public FinditResults As New IniFile("")
    Public AgentWorkspace As String ' Where all files for/from Agents are stored
    Public SessionWorkspace As String ' Where the files are really stored - each session in a nwe folder
    ' Where the utf8 files for the agent are stored
    Public sUTFpath As String 'Defined by Findit6 Settings ! 

    ' Relevant Values to define within the Agents ini
    Public iMaxStatExcerptKB As Integer 'Max size of excerpt-collections in AgentMode

    ' The colours used within the ui
    Public Class Colors
        ' Colors
        Public Background As Color = ColorTranslator.FromHtml("#4A5159")
        Public lightBackgrnd As Color = Color.Gray
        Public TxtBackgrnd As Color = ColorTranslator.FromHtml("#2A2F36")
        Public Buttons As Color = ColorTranslator.FromHtml("#626973")
        Public UsrTxt As Color = Color.LightGreen
        Public KiTXT As Color = Color.Black
        Public LnkTxt As Color = Color.Blue
        Public Text As Color = Color.White
        Public lightgreen As Color = Color.LightGreen
    End Class
    Public clr As New Colors

    'Definition of required data for any provider
    Public Class ProviderFieldDefinition
        Public Property Key As String
        Public Property ControlType As String
        Public Property Required As Boolean
    End Class

    Public providerValues As New Dictionary(Of String, String)

    Public Class RerankHit
        Public Property FilePath As String = ""
        Public Property LastModified As DateTime
        Public Property Score As Double = 0

        Public Property IsProbableDuplicate As Boolean = False
        Public Property DuplicateOfFilePath As String = ""
        Public Property DuplicatePercentage As Double = 0
    End Class

    Public Enum RerankerStartupState
        NotStarted
        Starting
        Ready
        Failed
    End Enum

    Public Enum ChunkActionEnum
        Idle
        QueryBUILDING   ' We start building the query, may take a while
        FinditRUNNING   ' Findit6 is running, Agent preparing Rerank-Queries
        Rerank          ' Reranker ist starting and - when Findit is reday: filelist for Rerank batches
        RerankBATCH     ' Send a Rerankbatch
        RerankWORKING   ' Working on RerankBatch
        RerankCleanUp
        RerankCleaning
        SessionRestart
        ExcerptsNEXT  ' All Reranks ready, start excerpting
        ExcerptsRUN
        ExcerptCANCELLED
        ExcerptRESTART
        SynthesisSTART
        Synthesis
        FinishedALL
        WaitForNext
    End Enum
    Public CurrentAction As ChunkActionEnum = ChunkActionEnum.QueryBUILDING

    Public m_RerankerStartupState As RerankerStartupState = RerankerStartupState.NotStarted
    Public ReadOnly m_RerankerStateLock As New Object
    Public m_RerankerStartupError As String = ""
    Public m_RerankerProcess As Process

    Public Class DetailQuestionItem
        Public Property Question As String = ""
        Public Property RerankQuery As String = ""
    End Class

    Public Class SynonymEntry

        ' Das unmittelbar vor der Klammer stehende Wort.
        Public Property OriginalWord As String = ""

        ' Unveränderter Inhalt der Klammer.
        Public Property RawContent As String = ""

        ' Bereinigter Inhalt ohne /, Komma, or, oder usw.
        Public Property SynonymWords As String = ""

    End Class

    Public Class TAgentLedger
        Public Property Name As String = ""
        Public Property Purpose As String = ""
        Public Property ResultUnit As String = ""
        Public Property Entries As New Dictionary(Of String, TAgentLedgerEntry)(StringComparer.OrdinalIgnoreCase)
    End Class

    Public Class TAgentLedgerEntry
        Public Property FilePath As String = ""
        Public Property FileDate As DateTime?
        Public Property Facts As String = ""
    End Class

    Public Class Sessionquery

        Public Type As String = ""

        Public bStopQuerry As Boolean = False
        Public StopReason As String = ""

        Public bAgentRunning As Boolean = False

        Public UserQuestion As String = ""
        Public Property TechTerms As New List(Of String)
        Public Property Synonyms As New List(Of SynonymEntry)

        Public TimeRestriction As String = ""

        Public CoreEntity As String = ""

        Public CoreEntitiesLogical As String = ""

        Public CoreAspects As String = ""

        Public TruncationTemplate As String = ""

        Public TruncationList As String = ""  'Alle Worte, die trunkiert werden müssen

        Public TruncationItems As New List(Of TruncationItem) 'Liste der zu trunkierenden Worte

        Public TruncAct As String = "" ' Das wort, aktuell trunkiert wird

        Public TruncNumber As Integer = 0 ' Die aktuelle Position des Wortes in der Liste

        Public untruncated As String = ""

        Public AIquestion As String = ""

        Public DetailQuestions As New List(Of DetailQuestionItem)

        Public RerankStart As DateTime

        Public HighestRank As Double = 0

        Public RerankList As New List(Of String)

        Public RerankCounter As Integer = 0 ' Wievielte Datei einer Liste im Reranker?

        Public CurrentRerankNr As Integer = 0 ' Wievielte Frage wird im Reranker gerade bearbeitet?

        Public RerankResults As New Dictionary(Of String, List(Of RerankHit))

        Public ExcerptQuestionIndex As Integer = 0

        Public ExcerptHitIndex As Integer = 0

        Public ExcerptOutputFile As String = ""

        Public ExcerptPromptStarted As DateTime

        Public ExcerptWholeSendedSize As Integer

        Public excerptSize As Integer = 0

        Public ExcerptFile As String = ""

        Public ExcerptText As String = ""
        Public irrelevantExcerptCount As Integer = 0

        Public sLastSentDoc As String

        Public Ledgers As New Dictionary(Of String, TAgentLedger)(StringComparer.OrdinalIgnoreCase)
    End Class
    Public query As New Sessionquery

    Public Class TruncationItem
        Public Property Placeholder As String
        Public Property Original As String
        Public Property Baseform As String
        Public GenderForm As String = ""
        Public Property Result As String
        Public Property WordType As String
    End Class

    Public Enum AI_Mood
        Logical
        Creative
        Reasoning
        Precise
        Defensive
    End Enum

    ' All data concerning the current LLM-Sesssion
    Public Class SessionData
        ' Some information abour User for AgentMode
        Public Property UserName As String
        Public Property UserContext As String

        ' State of AI Connection and which mode we are in

        Public bActive As Boolean = False ' Actually connected ?

        Public Sessionmode As String = ""  '"CHUNK" or "AGENT"

        Public ChunkMode As String = "" ' "", "READ", "EXTRACT"

        Public Userlanguage As String = "" 'D für Deutsch, wird aber über mds vom Agent gesetzt

        Public blocal As Boolean = False ' Are we working local ? 

        Public sPendingUserPrompt As String = ""

        ' Trimming of local Models

        Public AImood As AI_Mood = AI_Mood.Logical

        Public temp As Double = 0.6

        Public UseThinking As Boolean = False
        Public Property Keepalive As String = "5m"
        Public Property seed As Integer = 41

        'Some information about  actual searches, excerpt etcpp

        Public ActualExcpertRanking As Long

        Public SearchTerm As String = ""

        Public MaxExcerptLength As Integer = 45000

        Public DublicateThreshold As Integer = 80

        Public sExcerptsSize As String = ""

        Public AgentResearchMode As String = ""

        ' Information about provider and connection
        Public Property Provider As String

        Public Property BaseUrl As String
        Public Property Model As String
        Public Property ApiKey As String
        Public Property EnvironmentId As String
        Public Property EnvironmentName As String
        Public Property AgentId As String
        Public Property AgentName As String
        Public Property SessionId As String
        Public Property SessionStartTime As DateTime
        Public Property TokensIn As Long
        Public Property TokensOut As Long
        Public Property RateLimitWarningLevel As Integer
    End Class
    Public currentSession As New SessionData
    Public currentProvider As IAgentProvider

    Public Class PathGroup
        Public Number As String
        Public Mode As Integer
        Public Name As String
        Public Description As String
        Public PathCount As Integer  ' EXPLIZIT gespeichert
        Public Paths As New List(Of String)
    End Class
    Public SelectedPathgroup As PathGroup
    ' Variables used only during AgentMode
    Public bSearchStrategyReady As Boolean
    Public Const CONTEXT_LIMIT_MESSAGE As String = "This session has reached the maximum context size allowed by the AI provider. This limit applies to the AI session itself and is not imposed by Findit6Agent. Please start a new session to continue working."
    Public bContextLimit As Boolean = False
    Public iWaitingForLedger As Integer = 0
    ' State of processing
    Public bFinditRunning As Boolean = False ' we still need it for the Agent mode
    Public NewLineForRTF As String = ""
    Public bNoThink As Boolean = False
    Public iMaxHits As Integer = 1250 'Maximum of found files to process may be adapted by the user
    Public iFinditFoundFiles As Integer = 0
    Public sFinditResultfile As String 'what file its working on
    Public bworkerbusy As Boolean = False  'flag: worker busy
    Public lastStatusSentTime As DateTime
    Public bTimeoutRecovery As Boolean = False
    Public bReminderPending As Boolean = False
    Public dtLastAgentResponse As Date  'When did we receive the latest response?
    Public sLastAgentResponseType As String 'What type Action was requested (send_fij/readfile/waiting/text)
    Public dtLastPromptSent As Date ' When did we send our last prompt
    Public sTypeOfLastPrompt As String 'What type of Request did we sent last time? (starting/status/result/requestState/filetoread/text)
    Public sPromptToProcess As String ' The complete Prompt to send next
    Public bBiglogs As Boolean ' 'If true prompts, Answers und some more additional details are logged
    Public sPreviousPompt As String ' in case it was was lost
    Public KIresponse As String = ""
    Public bInvalidJson As Boolean
    Public iPromptBalance As Integer 'any prompt should be followed by message from KI
    Public sErrMessage As String = ""
    ' Not used here - but within Findit6 wich uses same form
    ' Liste von zu ignorierenden Ordnern
    Friend arrIgnore As New ArrayList '- Userdefiniert
    Friend arrBlockListe As New ArrayList ' - Admindefiniert
    'Logging    
    Public debugLogPath As String

End Module
