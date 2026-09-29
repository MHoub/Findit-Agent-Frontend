Option Explicit On
Option Strict On

Public NotInheritable Class c_ProviderFactory

    ' Creates the provider implementation selected for the current session.
    ' New provider types only need to be added to this central factory switch.
    Private Sub New()
    End Sub

    Public Shared Function CreateProvider() As IAgentProvider

        Select Case currentSession.Provider.Trim().ToLowerInvariant()

            Case "anthropic", "anthrophic"
                currentSession.blocal = False
                Return New AnthropicProvider()

            Case "openai", "open ai", "perplexity"
                currentSession.blocal = False
                Return New OpenAIProvider()

            Case "ollama"
                currentSession.blocal = True
                Return New c_OllamaProvider()

            Case "mistral", "openrouter", "groq", "together", "deepinfra", "grok", "gemini"
                currentSession.blocal = False
                Return New OpenAICompatibleProvider()

            Case Else
                Throw New NotSupportedException("Unbekannter Provider: " & currentSession.Provider)

        End Select

    End Function

End Class