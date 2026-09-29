Option Explicit On
Option Strict On

Imports System.Threading

Public NotInheritable Class ProviderInferenceSettings
    Public Property Mood As AI_Mood
    Public Property Think As Boolean
    Public Property Temperature As Double
End Class

Public Interface IAgentProvider
    ' No matter what KI we have connected
    ' No Matter wheater it local or somewhere else
    ' HERE we take care tha whe use the right form of Communkation 
    ' Some simple essential functions

    Function ConnectAsync(
        cancellationToken As CancellationToken) As Task

    Function SendPromptAsync(
    prompt As String,
    cancellationToken As CancellationToken,
    Optional clearHistory As Boolean = False,
    Optional inferenceSettings As ProviderInferenceSettings = Nothing) As Task

    Function ReceiveAsync(
        cancellationToken As CancellationToken) As Task(Of String)

    Function DisconnectAsync(
        cancellationToken As CancellationToken) As Task

    ReadOnly Property IsConnected As Boolean

    Function CloseSessionAsync(cancellationToken As CancellationToken) As Task

End Interface

Public MustInherit Class AgentProviderBase
    Implements IAgentProvider
    ' here we are filling this  functions with life

    Private _isConnected As Boolean

    Public MustOverride Function SendPromptAsync(
    prompt As String,
    cancellationToken As CancellationToken,
    Optional clearHistory As Boolean = False,
    Optional inferenceSettings As ProviderInferenceSettings = Nothing) As Task Implements IAgentProvider.SendPromptAsync

    Public MustOverride Function ReceiveAsync(cancellationToken As CancellationToken) As Task(Of String) Implements IAgentProvider.ReceiveAsync

    Protected MustOverride Function ProviderConnectAsync(cancellationToken As CancellationToken) As Task

    Protected MustOverride Function ProviderDisconnectAsync(cancellationToken As CancellationToken) As Task

    Public MustOverride Function CloseSessionAsync(cancellationToken As CancellationToken) As Task Implements IAgentProvider.CloseSessionAsync
    Public ReadOnly Property IsConnected As Boolean Implements IAgentProvider.IsConnected
        Get
            Return _isConnected
        End Get
    End Property

    Public Async Function ConnectAsync(cancellationToken As CancellationToken) As Task Implements IAgentProvider.ConnectAsync
        If _isConnected Then Return
        cancellationToken.ThrowIfCancellationRequested()
        Await ProviderConnectAsync(cancellationToken)
        _isConnected = True
    End Function

    Public Async Function DisconnectAsync(cancellationToken As CancellationToken) As Task Implements IAgentProvider.DisconnectAsync
        If Not _isConnected Then Return
        cancellationToken.ThrowIfCancellationRequested()
        Await ProviderDisconnectAsync(cancellationToken)
        _isConnected = False
    End Function

End Class
