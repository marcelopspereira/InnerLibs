﻿Imports System.Collections.ObjectModel
Imports System.Data.Common
Imports System.Data.Linq
Imports System.Data.Linq.Mapping
Imports System.IO
Imports System.Linq.Expressions
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Text.RegularExpressions
Imports InnerLibs.HtmlParser

Namespace Templatizer

    ''' <summary>
    ''' Gera HTML dinâmico a partir de uma conexão com banco de dados e um template HTML. Utiliza <see cref="Data.Linq.DataContext"/> como ponto de entrada para conexões.
    ''' </summary>
    ''' <typeparam name="DataContextType">Tipo da conexão com o banco de dados</typeparam>
    Public Class Templatizer(Of DataContextType As DataContext)

        ''' <summary>
        ''' Tipo de <see cref="Data.Linq.DataContext"/> utilizado para a comunicação com o banco
        ''' </summary>

        Private DataContext As DataContextType

        ''' <summary>
        ''' Pasta contendo os arquivos HTML e SQL utilizados como template
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property TemplateDirectory As DirectoryInfo = Nothing

        ''' <summary>
        ''' Aplicaçao contendo os Resources (arquivos compilados internamente) dos arquivos HTML e SQL
        ''' utilizados como template
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property ApplicationAssembly As Assembly = Nothing

        ''' <summary>
        ''' Lista contento as associações das classes aso seus arquivos de template
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property TemplateMap As New Dictionary(Of Type, String)

        ''' <summary>
        ''' Mapeia um template para um tipo
        ''' </summary>
        ''' <param name="Type"></param>
        ''' <returns></returns>
        Default Public Property MapType(Type As Type) As String
            Get
                If TemplateMap.ContainsKey(Type) Then
                    Return TemplateMap(Type)
                Else
                    Return ""
                End If
            End Get
            Set(value As String)
                TemplateMap(Type) = value
            End Set
        End Property


        ''' <summary>
        ''' Valores adicionados ao processamento do template que não vem do banco
        ''' </summary>
        ''' <returns></returns>
        Public Property CustomValues As New Dictionary(Of String, Object)




        ''' <summary>
        ''' Retorna o nome do arquivo de template
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <returns></returns>
        Function GetTemplate(Of T As Class)(Optional ProcessFile As Boolean = False) As String
            Return If(ProcessFile, GetTemplateContent(MapType(GetType(T))), MapType(GetType(T)))
        End Function



        ''' <summary>
        ''' Aplica um arquivo de template a um tipo
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="Template"></param>
        ''' <returns></returns>
        Function SetTemplate(Of T As Class)(Template As String) As Templatizer(Of DataContextType)
            MapType(GetType(T)) = Template
            Return Me
        End Function


        Sub New(TemplateDirectory As DirectoryInfo, Optional DateTimeFormat As String = "dd/MM/yyyy hh:mm:ss")
            Me.DateTimeFormat = If(DateTimeFormat, "dd/MM/yyyy hh:mm:ss")
            Me.TemplateDirectory = TemplateDirectory
            Me.DataContext = Activator.CreateInstance(Of DataContextType)
        End Sub

        Sub New(ApplicationAssembly As Assembly, Optional DateTimeFormat As String = "dd/MM/yyyy hh:mm:ss")
            Me.DateTimeFormat = If(DateTimeFormat, "dd/MM/yyyy hh:mm:ss")
            Me.ApplicationAssembly = ApplicationAssembly
            Me.DataContext = Activator.CreateInstance(Of DataContextType)
        End Sub

        ''' <summary>
        ''' Formato das datas apresentadas no template
        ''' </summary>
        ''' <returns></returns>
        Public Property DateTimeFormat As String
            Get
                Return _datetimeformat.IfBlank("dd/MM/yyyy hh:mm:ss")
            End Get
            Set(value As String)
                _datetimeformat = value.IfBlank("dd/MM/yyyy hh:mm:ss")
            End Set
        End Property
        Private _datetimeformat As String = "dd/MM/yyyy hh:mm:ss"


        ''' <summary>
        ''' Executa uma query SQL e retorna um <see cref="IEnumerable"/> com os resultados (É um wrapper para <see cref="datacontext.ExecuteQuery(Of TResult)(String, Object())"/> porém aplica os templates automaticamente
        ''' </summary>
        ''' <typeparam name="T">Tipo do Objeto</typeparam>
        ''' <param name="SQLQuery"></param>
        ''' <param name="Parameters"></param>
        ''' <returns></returns>
        Public Function ApplyTemplate(Of T As Class)(SQLQuery As String, Optional Template As String = "", Optional Parameters As Object() = Nothing, Optional PageNumber As Integer = 1, Optional PageSize As Integer = 0) As TemplateList(Of T)
            Debug.WriteLine(Environment.NewLine & SQLQuery)
            Dim list As IEnumerable(Of T)
            Me.DataContext = Activator.CreateInstance(Of DataContextType)
            Using Me.DataContext
                If GetType(T) = GetType(Dictionary(Of String, Object)) Then
                    Dim con As DbConnection = Activator.CreateInstance(Me.DataContext.Connection.GetType)
                    con.ConnectionString = Me.DataContext.Connection.ConnectionString
                    con.Open()
                    Dim command As DbCommand = con.CreateCommand()
                    command.CommandText = SQLQuery
                    Using Reader As DbDataReader = command.ExecuteReader()
                        Dim l As New List(Of Dictionary(Of String, Object))
                        While Reader.Read
                            Dim d As New Dictionary(Of String, Object)
                            For i As Integer = 0 To Reader.FieldCount - 1
                                d.Add(Reader.GetName(i), Reader(Reader.GetName(i)))
                            Next
                            l.Add(d)
                        End While
                        list = l.AsEnumerable
                        Reader.Dispose()
                    End Using
                    con.Close()
                Else
                    list = DataContext.ExecuteQuery(Of T)(SQLQuery, If(Parameters, {}))
                End If
                Return ApplyTemplate(Of T)(list, Template, PageNumber, PageSize)
            End Using
        End Function

        ''' <summary>
        ''' Aplica um template a um unico item
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="Item"></param>
        ''' <returns></returns>
        Public Function ApplyTemplate(Of T As Class)(Item As T, Optional Template As String = "") As Template(Of T)
            If Template.IsBlank Then
                Template = Me.GetTemplate(Of T)
            End If
            Template = "" & GetTemplateContent(Template)
            If Template.IsNotBlank Then
                Template = ReplaceValues(Item, Template)
                Template = ReplaceValues(CustomValues, Template)
                Template = ProccessConditions(Item, Template)
                Template = ProccessSwitch(Item, Template)
                Template = ProccessIf(Item, Template)
                Template = ProcessSubClass(Item, Template)
                Template = ProccessSubTemplate(Item, Template)
                Template = ClearValues(Item, Template)
            End If
            Return New Template(Of T)(Item, Template)
        End Function

        Public Function ApplyTemplate(Of T As Class)(List As Data.Linq.Table(Of T), Optional Template As String = "", Optional PageNumber As Integer = 1, Optional PageSize As Integer = 0) As TemplateList(Of T)
            Return ApplyTemplate(List.AsQueryable, Template, PageNumber, PageSize)
        End Function

        Public Function ApplyTemplate(Of T As Class)(List As Data.Linq.ISingleResult(Of T), Optional Template As String = "", Optional PageNumber As Integer = 1, Optional PageSize As Integer = 0) As TemplateList(Of T)
            Return ApplyTemplate(List.AsQueryable, Template, PageNumber, PageSize)
        End Function

        Public Function ApplyTemplate(Of T As Class)(List As IQueryable(Of T), Optional Template As String = "", Optional PageNumber As Integer = 1, Optional PageSize As Integer = 0) As TemplateList(Of T)
            Dim total = List.Count
            If PageSize < 1 Then PageSize = total
            PageNumber = PageNumber.LimitRange(1, (total / PageSize).ChangeType(Of Decimal).Ceil())
            Dim l As New List(Of Template(Of T))
            For Each item As T In List.Page(PageNumber, PageSize)
                l.Add(ApplyTemplate(Of T)(CType(item, T), Template))
            Next
            Return New TemplateList(Of T)(l, PageSize, PageNumber, total)
        End Function

        ''' <summary>
        ''' Aplica um template a uma lista
        ''' </summary>
        ''' <typeparam name="T">Tipo do item</typeparam>
        ''' <param name="List">Lista</param>
        ''' <param name="Template">Nome ou thml do template</param>
        ''' <returns></returns>
        Public Function ApplyTemplate(Of T As Class)(List As IEnumerable(Of T), Optional Template As String = "", Optional PageNumber As Integer = 1, Optional PageSize As Integer = 0) As TemplateList(Of T)
            Dim total = List.Count
            If PageSize < 1 Then PageSize = total
            PageNumber = PageNumber.LimitRange(1, (total / PageSize).ChangeType(Of Decimal).Ceil())
            Dim l As New List(Of Template(Of T))
            For Each item As T In List.Page(PageNumber, PageSize)
                l.Add(ApplyTemplate(Of T)(CType(item, T), Template))
            Next
            Return New TemplateList(Of T)(l, PageSize, PageNumber, total)
        End Function





        ''' <summary>
        ''' Retorna o conteudo estático de um arquivo de template
        ''' </summary>
        ''' <param name="Templatefile">Nome do arquivo do template</param>
        ''' <param name="HeadTag">TRUE para trazer o conteudo fixo do template (head), FALSE para trazer o conteudo dinamico do template (body)</param>
        ''' <returns></returns>
        Public Function GetTemplateContent(TemplateFile As String, Optional Headtag As Boolean = False) As String
            If TemplateFile.IsNotBlank Then
                If TemplateFile.ContainsAny("<", ">") Then
                    Try
                        Return New HtmlDocument(TemplateFile).HTML
                    Catch ex As Exception
                        Throw New Exception("Error on parsing Template String")
                    End Try
                Else
                    TemplateFile = Path.GetFileNameWithoutExtension(TemplateFile) & ".html"
                    If IsNothing(ApplicationAssembly) Then
                        Dim filefound = TemplateDirectory.SearchFiles(SearchOption.TopDirectoryOnly, TemplateFile).First
                        If Not filefound.Exists Then Throw New FileNotFoundException(TemplateFile.Quote & "  not found in " & TemplateDirectory.Name.Quote)
                        Using file As StreamReader = filefound.OpenText
                            Try
                                Return CType(New HtmlDocument(file.ReadToEnd).Nodes.GetElementsByTagName(If(Headtag, "head", "body"))(0), HtmlElement).InnerHTML
                            Catch ex As Exception
                                Throw New Exception(If(Headtag, "head", "body") & " not found in " & filefound.Name)
                            End Try
                        End Using
                    Else
                        Try
                            Dim txt = GetResourceFileText(ApplicationAssembly, ApplicationAssembly.GetName.Name & "." & TemplateFile)
                            Try
                                Return CType(New HtmlDocument(txt).Nodes.GetElementsByTagName(If(Headtag, "head", "body"))(0), HtmlElement).InnerHTML
                            Catch ex As Exception
                                Throw New Exception(If(Headtag, "head", "body") & " not found in " & ApplicationAssembly.GetName.Name & "." & TemplateFile)
                            End Try
                        Catch ex As Exception
                            Throw New FileNotFoundException(TemplateFile.Quote & "  not found in " & ApplicationAssembly.GetName.Name.Quote & " resources. Check if Build Action is marked as ""Embedded Resource"" in File Properties.")
                        End Try
                    End If
                End If
            End If
            Return ""
        End Function

        ''' <summary>
        ''' Pega o comando SQL de um arquivo ou resource
        ''' </summary>
        ''' <param name="CommandFile">Nome do arquivo ou resource</param>
        ''' <returns></returns>
        Function GetCommand(CommandFile As String) As String
            CommandFile = Path.GetFileNameWithoutExtension(CommandFile) & ".sql"
            Select Case True
                Case IsNothing(ApplicationAssembly) And Not IsNothing(TemplateDirectory)
                    Dim filefound = TemplateDirectory.SearchFiles(SearchOption.TopDirectoryOnly, CommandFile).First
                    If Not filefound.Exists Then Throw New FileNotFoundException(CommandFile.Quote & "  not found in " & TemplateDirectory.Name.Quote)
                    Using file As StreamReader = filefound.OpenText
                        Return file.ReadToEnd
                    End Using
                Case Not IsNothing(ApplicationAssembly) And IsNothing(TemplateDirectory)
                    Try
                        Return GetResourceFileText(ApplicationAssembly, ApplicationAssembly.GetName.Name & "." & CommandFile)
                    Catch ex As Exception
                        Throw New FileNotFoundException(CommandFile.Quote & "  not found in " & ApplicationAssembly.GetName.Name.Quote & " resources. Check if Build Action is marked as ""Embedded Resource"" in File Properties.")
                    End Try
                Case Else
                    Throw New Exception("ApplicationAssembly or CommandDirectory is not configured!")
            End Select
        End Function


        Private sel As String() = {"##", "##"}

        ''' <summary>
        ''' Seletor dos campos do template
        ''' </summary>
        ''' <returns></returns>
        Function GetFieldSelector() As String()
            Return sel
        End Function

        ''' <summary>
        ''' Aplica os seletores deste Templatizer a uma string (nome do campo)
        ''' </summary>
        ''' <param name="Name"></param>
        ''' <returns></returns>
        Public Function ApplySelector(Name As String) As String
            Return sel(0) & Name & sel(1)
        End Function

        ''' <summary>
        ''' Altera os seletores padrão dos campos
        ''' </summary>
        ''' <param name="OpenSelector"> Seletor</param>
        ''' <param name="CloseSelector"></param>
        Public Sub SetSelector(OpenSelector As String, CloseSelector As String)
            sel(0) = If(OpenSelector.IsBlank, "##", OpenSelector)
            sel(1) = If(CloseSelector.IsBlank, "##", CloseSelector)
        End Sub


        Friend Function ProccessConditions(Of t As Class)(Item As t, Template As String) As String
            Dim doc As New HtmlDocument(Template)
            For Each conditionTag As HtmlElement In doc.Nodes.GetElementsByTagName("condition", True)
                Try
                    Dim expression = CType(conditionTag.Nodes.GetElementsByTagName("expression", False)(0), HtmlElement).InnerHTML.HtmlDecode
                    Dim contenttag = CType(conditionTag.Nodes.GetElementsByTagName("content", False)(0), HtmlElement).InnerHTML.HtmlDecode
                    Dim resultexp = EvaluateExpression(expression)

                    If resultexp = True Or resultexp > 0 Then
                        conditionTag.Mutate(contenttag)
                    Else
                        conditionTag.Destroy()
                    End If
                Catch ex As Exception
                    conditionTag.Destroy()
                End Try
            Next
            Template = doc.InnerHTML
            Return Template
        End Function

        Friend Function ProccessIf(Of t As Class)(Item As t, Template As String) As String
            Dim doc As New HtmlDocument(Template)
            For Each conditionTag As HtmlElement In doc.Nodes.GetElementsByTagName("IF", True)
                Try
                    Dim expression = CType(conditionTag.Nodes.GetElementsByTagName("expression")(0), HtmlElement).InnerHTML.HtmlDecode
                    Dim truetag = CType(conditionTag.Nodes.GetElementsByTagName("true", False)(0), HtmlElement).InnerHTML.HtmlDecode
                    Dim falsetag = ""
                    Try
                        falsetag = CType(conditionTag.Nodes.GetElementsByTagName("false", False)(0), HtmlElement).InnerHTML.HtmlDecode
                    Catch
                    End Try
                    Dim resultexp = EvaluateExpression(expression)
                    If resultexp = True Or resultexp > 0 Then
                        conditionTag.Mutate(truetag)
                    Else
                        If falsetag.IsNotBlank Then
                            conditionTag.Mutate(falsetag)
                        Else
                            conditionTag.Destroy()
                        End If
                    End If
                Catch ex As Exception
                    conditionTag.Destroy()
                End Try
            Next
            Template = doc.InnerHTML
            Return Template
        End Function

        Friend Function ProccessSwitch(Of t As Class)(Item As t, Template As String) As String
            Dim doc As New HtmlDocument(Template)
            For Each conditionTag As HtmlElement In doc.Nodes.GetElementsByTagName("switch", True)
                Try
                    Dim othertag = conditionTag.FindElements(Function(n As HtmlElement)
                                                                 Dim value1 = conditionTag.Attribute("value").HtmlDecode
                                                                 Dim value2 = n.Attribute("value").HtmlDecode
                                                                 Dim op = n.Attribute("operator").HtmlDecode.IfBlank("=")
                                                                 value1 = value1.QuoteIf(Not value1.IsNumber)
                                                                 value2 = value2.QuoteIf(Not value2.IsNumber)
                                                                 Return n.Name = "case" AndAlso (EvaluateExpression(value1 & op & value2) = True)
                                                             End Function, False)
                    'Dim othertag = conditionTag.Find(Function(n As HtmlElement) n.Name = "case" AndAlso n.Attribute("value") = conditionTag.Attribute("value"), False)
                    Dim html = ""
                    If othertag.Count > 0 Then
                        For Each node As HtmlElement In othertag
                            html.Append(node.InnerHTML)
                            If node.HasAttribute("break") Then
                                Exit For
                            End If
                        Next
                    Else
                        othertag = conditionTag.FindElements(Function(n As HtmlElement) n.Name = "else", False)
                        If othertag.Count = 1 Then
                            html = CType(othertag.First, HtmlElement).InnerHTML
                        End If
                    End If

                    If html.IsNotBlank Then
                        conditionTag.Mutate(html)
                    Else
                        conditionTag.Destroy()
                    End If
                Catch ex As Exception
                    conditionTag.Destroy()
                End Try
            Next
            Template = doc.InnerHTML
            Return Template
        End Function


        Friend Function ProccessSubTemplate(Of t As Class)(item As t, Template As String) As String
            Dim doc As New HtmlDocument(Template)
            For Each templatetag As HtmlElement In doc.Nodes.GetElementsByTagName("template", True)
                Try
                    Dim sql = CType(templatetag.Nodes.GetElementsByTagName("sql")(0), HtmlElement).InnerHTML.HtmlDecode
                    Dim conteudo = CType(templatetag.Nodes.GetElementsByTagName("content")(0), HtmlElement).InnerHTML.HtmlDecode
                    Dim lista = ApplyTemplate(Of Dictionary(Of String, Object))(sql, conteudo)
                    conteudo = lista.BuildHtml
                    templatetag.Mutate(conteudo)
                Catch ex As Exception
                    templatetag.Destroy()
                End Try
            Next
            Template = doc.InnerHTML
            Return Template
        End Function


        Friend Function ProcessSubClass(Of T As Class)(Item As T, Template As String) As String
            Dim doc As New HtmlDocument(Template)
            For Each class_tag As HtmlElement In doc("[templatizer-source]")
                Try
                    Dim newcontent = ""
                    Dim lis = Item.GetPropertyValue(class_tag.Attribute("templatizer-source"))
                    If lis IsNot Nothing Then
                        For Each el In lis
                            newcontent = ReplaceValues(el, class_tag.InnerHTML)
                        Next
                    End If
                    class_tag.Mutate(newcontent)
                Catch ex As Exception
                    class_tag.Destroy()
                End Try
            Next
            Template = doc.InnerHTML
            Return Template
        End Function

        Private Function GetRegexPattern() As String
            Dim open = Me.sel(0).ToArray.Join("\").Prepend("\")
            Dim close = Me.sel(1).ToArray.Join("\").Prepend("\")
            Return open & "(.*?)" & close
        End Function

        Friend Function ReplaceValues(Of T As Class)(Item As T, Template As String) As String
            Template = GetTemplateContent(Template)
            If Template.IsNotBlank Then
                Dim ff As MatchEvaluator
                Dim pt = GetRegexPattern()
                Dim re As Regex = New Regex(pt, RegexOptions.Compiled)
                If GetType(T) = GetType(Dictionary(Of String, Object)) AndAlso CType(CType(Item, Object), Dictionary(Of String, Object)).Count > 0 Then
                    ff = Function(match)
                             Dim s As String
                             Try
                                 s = CType(Item, IDictionary)(match.Groups(1).Value)
                             Catch ex As Exception
                                 s = ApplySelector(match.Groups(1).Value)
                             End Try
                             Return s
                         End Function
                Else
                    If Item.GetProperties.Count > 0 Then
                        ff = Function(match As Match) As String
                                 Dim val = Item.GetPropertyValue(Of Object)(match.Groups(1).Value, True)
                                 If val Is Nothing Then Return ApplySelector(match.Groups(1).Value)
                                 If val.GetType.IsIn({GetType(Date), GetType(Date?)}) Then
                                     Dim d As Date? = val
                                     If d.HasValue Then
                                         Return d.Value.ToString(DateTimeFormat)
                                     Else
                                         Return ""
                                     End If
                                 Else
                                     Return val.ToString
                                 End If
                             End Function
                    End If
                End If
                Template = re.Replace(Template, ff)
            End If
            Return Template
        End Function

        Friend Function ClearValues(Of T As Class)(Item As T, Template As String)
            Template = GetTemplateContent(Template)
            If Template.IsNotBlank Then
                Dim ff As MatchEvaluator
                Dim pt = GetRegexPattern()
                Dim re As Regex = New Regex(pt, RegexOptions.Compiled)
                ff = Function(match)
                         Return ""
                     End Function
                Template = re.Replace(Template, ff)
            End If
            Return Template
        End Function
    End Class






    ''' <summary>
    ''' Estrutura de template do templatizer
    ''' </summary>
    ''' <typeparam name="T"></typeparam>
    Public Class Template(Of T)
        ''' <summary>
        ''' Cria um novo template
        ''' </summary>
        ''' <param name="Data"></param>
        ''' <param name="ProcessedTemplate"></param>
        Sub New(Data As T, ProcessedTemplate As String)
            Me.Data = Data
            Me.ProcessedTemplate = ProcessedTemplate
        End Sub

        ''' <summary>
        ''' Informação do template
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property Data As T
        ''' <summary>
        ''' Template processado
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property ProcessedTemplate As String
    End Class



    Public Class TemplateList(Of T As Class)
        Inherits ReadOnlyCollection(Of Template(Of T))

        Friend Sub New(lista As List(Of Template(Of T)), Pagesize As Integer, PageNumber As Integer, Total As Integer)
            MyBase.New(lista)
            Me.PageSize = Pagesize
            Me.PageNumber = PageNumber
            Me.Total = Total
        End Sub

        Property EmptyListPlaceholder As String = ""

        ''' <summary>
        ''' Numero de Itens por pagina
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property PageSize As Integer

        ''' <summary>
        ''' Numero da pagina que corresponde a esta instancia
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property PageNumber As Integer

        ''' <summary>
        ''' Total de Itens para este template
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property Total As Integer

        ''' <summary>
        ''' Numero de Paginas deste template
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property PageCount As Integer
            Get
                Return (Total / PageSize).ChangeType(Of Decimal).Ceil
            End Get
        End Property

        ''' <summary>
        ''' Retorna o HTML de uma lista de templates
        ''' </summary>
        ''' <returns></returns>
        Public Overrides Function ToString() As String
            Dim html As String = ""
            For Each i In Me
                html &= i.ProcessedTemplate
            Next
            Return html.IfBlank(EmptyListPlaceholder)
        End Function

        ''' <summary>
        ''' Retorna o HTML de uma lista de templates
        ''' </summary>
        ''' <returns></returns>
        Public Function BuildHtml() As String
            Return Me.ToString
        End Function


    End Class


End Namespace


