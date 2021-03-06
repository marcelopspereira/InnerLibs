﻿
Imports System.Globalization
Imports System.Runtime.CompilerServices

''' <summary>
''' Wrapper para criaçao de gráficos em DOT Language
''' </summary>
Public Class Digraph
    Inherits List(Of DotObject)


    Property Clusters As List(Of Cluster)


    ''' <summary>
    ''' Tipo do Grafico
    ''' </summary>
    ''' <returns></returns>

    ReadOnly Property GraphType As String = "digraph"

    ''' <summary>
    ''' Nome do Gráfico
    ''' </summary>
    ''' <returns></returns>
    Property ID As String = ""

    ''' <summary>
    ''' Escreve a DOT string correspondente a este gráfico
    ''' </summary>
    ''' <returns></returns>
    Public Overrides Function ToString() As String
        Dim s = Me.Select(Function(n) n.ToString & Environment.NewLine).ToArray.Join("")
        s = s.Split(Environment.NewLine).Distinct.Join(Environment.NewLine) & Environment.NewLine
        Return GraphType & " " & ID.ToSlug(True) & " " & s.Wrap("{")
    End Function


End Class

Public Class Cluster
    Inherits DotObject

    Public Overrides Property ID As String
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As String)
            Throw New NotImplementedException()
        End Set
    End Property
End Class

Public MustInherit Class DotObject

    Public MustOverride Property ID As String

    Public ReadOnly Property Attributes As New DotAttributeCollection()

End Class

Public Class DotAttributeCollection
    Inherits Dictionary(Of String, Object)


    Public Overrides Function ToString() As String
        Dim dotstring = ""
        For Each prop In Me
            Dim val = prop.Value.ToString.QuoteIf(prop.Value.ToString.Contains(" ") Or prop.Value.ToString.IsBlank Or prop.Value.ToString.IsURL)
            If val.IsIn({"True", "False"}) Then val = val.ToLower
            If val.IsNumber Then val = Text.ToNumberString(CType(val, Decimal), "", ".")
            dotstring &= prop.Key & "=" & val & " "
        Next
        Return dotstring.Wrap("[") & ";" & Environment.NewLine
    End Function

End Class


''' <summary>
''' Representa um nó de um grafico em DOT Language
''' </summary>
Public Class DotNode
    Inherits DotObject

    ''' <summary>
    ''' Cria um novo nó
    ''' </summary>
    ''' <param name="ID"></param>
    Sub New(ID As String)
        Me.ID = ID
    End Sub


    ''' <summary>
    ''' ID deste nó
    ''' </summary>
    ''' <returns></returns>
    Public Overrides Property ID As String
        Get
            Return _id
        End Get
        Set(value As String)
            _id = value.ToSlug(True)
        End Set
    End Property
    Private _id As String

    ''' <summary>
    ''' Escreve a DOT string deste nó e seus respectivos nós filhos
    ''' </summary>
    ''' <returns></returns>
    Public Overrides Function ToString() As String
        Return ID & Me.Attributes.ToString & Environment.NewLine
    End Function

End Class

''' <summary>
''' Representa uma ligação entre nós de um grafico em DOT Language
''' </summary>
Public Class DotEdge
    Inherits DotObject

    ''' <summary>
    ''' Cria uma nova ligaçao 
    ''' </summary>
    ''' <param name="Oriented">Relação orientada</param>
    Sub New(ParentNode As DotNode, ChildNode As DotNode, Optional Oriented As Boolean = True)
        Me.ParentNode = ParentNode
        Me.ChildNode = ChildNode
        Me.Oriented = Oriented
    End Sub

    ''' <summary>
    ''' Indica se esta ligação é orientada ou não
    ''' </summary>
    ''' <returns></returns>
    Property Oriented As Boolean = True

    Property ParentNode As DotNode

    Property ChildNode As DotNode

    Public Overrides Property ID As String
        Get
            Return ParentNode.ID.ToSlug(True) & If(Me.Oriented, " -> ", " -- ") & ChildNode.ID.ToSlug(True)
        End Get
        Set(value As String)
            Debug.Write("Cannot change ID of a relation")
        End Set
    End Property

    ''' <summary>
    ''' Escreve a DOT String desta ligaçao
    ''' </summary>
    ''' <returns></returns>
    Public Overrides Function ToString() As String
        Dim dotstring = ""
        If Me.Attributes.Count > 0 Then
            dotstring = Me.ID & " " & Me.Attributes.ToString & Environment.NewLine
        End If
        Return dotstring
    End Function

End Class
