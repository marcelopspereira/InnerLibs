Imports System.Text
Imports System.ComponentModel
Imports System.Drawing
Imports System.Xml
Imports System.Web.UI.HtmlControls

Namespace HtmlParser

    ''' <summary>
    ''' The HtmlElement object represents any HTML element. An element has a name
    ''' and zero or more attributes.
    ''' </summary>
    Public Class HtmlElement
        Inherits HtmlNode
        Protected mName As String
        Protected mNodes As HtmlNodeCollection
        Protected mAttributes As HtmlAttributeCollection
        Protected mIsTerminated As Boolean
        Protected mIsExplicitlyTerminated As Boolean

        ''' <summary>
        ''' This constructs a new HTML element with the specified tag name.
        ''' </summary>
        ''' <param name="name">The name of this element</param>
        Public Sub New(name As String, Optional InnerHtml As String = "")
            mNodes = New HtmlNodeCollection(Me)
            mAttributes = New HtmlAttributeCollection(Me)
            mName = name
            mIsTerminated = False
            Style = New CssProperties(Me)
            [Class] = New ClassList(Me)
            If InnerHtml.IsNotBlank Then
                Me.InnerHTML = InnerHtml
            End If
        End Sub

        ''' <summary>
        ''' This constructs a new HTML element using a <see cref="HtmlGenericControl"/> as source.
        ''' </summary>
        ''' <param name="HtmlControl">The server control</param>
        Public Sub New(HtmlControl As HtmlGenericControl)
            Me.New(HtmlControl.TagName, HtmlControl.InnerHtml)
            For Each a In HtmlControl.Attributes.Keys
                If Not a.tolower = "innerhtml" Then Me.Attribute(a) = HtmlControl.Attributes(a)
            Next
        End Sub

        ''' <summary>
        ''' Create a <see cref="HtmlControl"/> using this <see cref="HtmlElement"/> as source
        ''' </summary>
        ''' <typeparam name="Type"></typeparam>
        ''' <returns></returns>
        Public Function CreateWebFormControl(Of Type As HtmlControl)() As Type
            Dim d As New HtmlGenericControl(Me.Name)
            For Each a In Me.Attributes
                d.Attributes(a.Name) = a.Value
            Next
            d.InnerHtml = Me.InnerHTML
            Return CType(CType(d, Object), Type)
        End Function

        ''' <summary>
        ''' Transform the current element into a new set of elements
        ''' </summary>
        ''' <param name="Elements">Collection of new elements</param>
        Sub Mutate(Elements As HtmlNodeCollection)
            Dim idx = Me.Index
            Me.Mutate(Elements.First)
            For i = 1 To Elements.Count - 1
                Me.Parent.Nodes.Insert(idx + i, Elements(i))
            Next
        End Sub

        ''' <summary>
        ''' Transform the current element into a new  element
        ''' </summary>
        ''' <param name="Element">New element</param>
        Sub Mutate(Element As HtmlElement)
            Me.Attributes.Clear()
            mAttributes = Element.Attributes
            Me.IsExplicitlyTerminated = Element.IsExplicitlyTerminated
            Me.InnerHTML = Element.InnerHTML
            Me.Name = Element.Name
        End Sub

        ''' <summary>
        ''' Transform the current element into a new  element or set of elements using a html string as source
        ''' </summary>
        ''' <param name="Html">Html String</param>
        Sub Mutate(Html As String)
            If Html.IsNotBlank Then
                Dim doc = New HtmlDocument(Html)
                Me.Mutate(doc.Nodes)
            Else
                Me.Destroy()
            End If
        End Sub

        ''' <summary>
        ''' Verify if this element has an specific attribute
        ''' </summary>
        ''' <param name="Name"></param>
        ''' <returns></returns>
        Public Function HasAttribute(Name As String) As Boolean
            Return Me.Attributes.Contains(Name)
        End Function

        ''' <summary>
        ''' Verify if this element has an specific class
        ''' </summary>
        ''' <param name="ClassName"></param>
        ''' <returns></returns>
        Public Function HasClass(ClassName As String) As Boolean
            Return If(ClassName.IsBlank, Me.Attribute("class").IsNotBlank, Me.Class(ClassName))
        End Function

        ''' <summary>
        ''' Remove this element from parent element. If parent element is null, nothing happens
        ''' </summary>
        Function Destroy() As Boolean
            If Me.Parent IsNot Nothing Then
                Me.Parent.Nodes.Remove(Me)
                Return Me.Parent.Nodes.Contains(Me)
            Else
                Return False
            End If
        End Function

        ''' <summary>
        ''' Travesse element with a CSS selector an retireve nodes
        ''' </summary>
        ''' <param name="CssSelector">Teh CSS selector</param>
        ''' <returns></returns>
        Default ReadOnly Property QuerySelectorAll(CssSelector As String) As HtmlNodeCollection
            Get
                Return Me.Nodes(CssSelector)
            End Get
        End Property

        ''' <summary>
        ''' The CSS style of element
        ''' </summary>
        ''' <returns></returns>
        <Category("General"), Description("The CSS style of this element"), TypeConverter(GetType(ExpandableObjectConverter))>
        Public ReadOnly Property Style As CssProperties

        ''' <summary>
        ''' Return the child elements of this element (excluding HtmlText)
        ''' </summary>
        ''' <returns></returns>
        <Category("General"), Description("The Child Elements of this element. Exclude Text Nodes")>
        ReadOnly Property ChildElements As HtmlNodeCollection
            Get
                Dim l As New HtmlNodeCollection(Parent)
                l.AddRange(Nodes.Where(Function(p) TypeOf p Is HtmlElement).Select(Function(p) CType(p, HtmlElement)))
                Return l
            End Get
        End Property

        ''' <summary>
        ''' Return the text elements of this element (excluding HtmlElement)
        ''' </summary>
        ''' <returns></returns>
        <Category("General"), Description("The associated text to this element. Exclude HTML Nodes")>
        Property ContentText As HtmlNodeCollection
            Get
                Dim l As New HtmlNodeCollection(Parent)
                l.AddRange(Nodes.Where(Function(p) TypeOf p Is HtmlText).Select(Function(p) CType(p, HtmlText)))
                Return l
            End Get
            Set(value As HtmlNodeCollection)
                If value.Count > 0 Then
                    Me.InnerText = value.Select(Function(p)
                                                    If TypeOf p Is HtmlText Then
                                                        Return p.ToString
                                                    Else
                                                        Return CType(p, HtmlElement).InnerText
                                                    End If
                                                End Function).ToArray.Join("")
                End If

            End Set
        End Property

        ''' <summary>
        ''' Gets os sets a boolean value for an specific class
        ''' </summary>
        ''' <returns></returns>
        <Category("General"), Description("The CSS class of this element"), TypeConverter(GetType(ExpandableObjectConverter))>
        Public ReadOnly Property [Class] As ClassList

        ''' <summary>
        ''' Return the value of specific attibute
        ''' </summary>
        ''' <param name="Name"></param>
        ''' <returns></returns>
        '''
        <Category("General"), Description("An especific attribute of element")>
        Property Attribute(Name As String) As String
            Get
                If Name.IsNotBlank Then
                    Try
                        Return Me.Attributes.Item(Name.ToLower).Value
                    Catch ex As Exception
                        Return ""
                    End Try
                End If
                Return ""
            End Get
            Set(value As String)
                If Name.IsNotBlank Then
                    If Me.Attributes.Where(Function(e) e.Name.ToLower = Name.ToLower).Count > 0 Then
                        Me.Attributes.Item(Name.ToLower).Value = value
                    Else
                        Me.Attributes.Add(New HtmlAttribute(Name.ToLower, value))
                    End If
                End If
            End Set
        End Property

        ''' <summary>
        ''' Retorna os nomes dos atributos
        ''' </summary>
        ''' <returns></returns>
        <Category("General"), Description("All attributes names of this element")>
        Public ReadOnly Property AttributesNames As IEnumerable(Of String)
            Get
                Return Me.Attributes.Select(Function(p) p.Name)
            End Get
        End Property

        ''' <summary>
        ''' This is the tag name of the element. e.g. BR, BODY, TABLE etc.
        ''' </summary>
        <Category("General"), Description("The name of the tag/element")>
        Public Property Name As String
            Get
                Return mName.ToLower
            End Get
            Set
                mName = Value.ToLower
            End Set
        End Property

        ''' <summary>
        ''' The ID of element
        ''' </summary>
        ''' <returns></returns>
        <Category("General"), Description("The ID of the tag/element")>
        Public Property ID As String
            Get
                Return Me.Attribute("id")
            End Get
            Set(value As String)
                Me.Attribute("id") = value
            End Set
        End Property

        ''' <summary>
        ''' This is the collection of all child nodes of this one. If this node is actually
        ''' a text node, this will throw an InvalidOperationException exception.
        ''' </summary>
        <Category("General"), Description("The set of child nodes")>
        Public ReadOnly Property Nodes() As HtmlNodeCollection
            Get
                If IsText() Then
                    Return Nothing
                End If
                Return mNodes
            End Get
        End Property

        ''' <summary>
        ''' This is the collection of attributes associated with this element.
        ''' </summary>
        <Category("General"), Description("The set of attributes associated with this element")>
        Public ReadOnly Property Attributes() As HtmlAttributeCollection
            Get
                Return mAttributes
            End Get
        End Property

        ''' <summary>
        ''' Gets os sets a value indicating thats element is disabled
        ''' </summary>
        ''' <returns></returns>
        Public Property Disabled As Boolean
            Get
                Return Me.HasAttribute("disabled")
            End Get
            Set(value As Boolean)
                If value Then
                    Me.Attributes.Add("disabled")
                Else
                    Me.Attributes.Remove("disabled")
                End If
            End Set
        End Property

        ''' <summary>
        ''' This flag indicates that the element is explicitly closed using the "<name/>" method.
        ''' </summary>
        Property IsTerminated As Boolean
            Get
                If Nodes.Count > 0 Then
                    Return False
                Else
                    Return mIsTerminated Or mIsExplicitlyTerminated
                End If
            End Get
            Set
                mIsTerminated = Value
                mIsExplicitlyTerminated = Not Value
            End Set
        End Property

        ''' <summary>
        ''' This flag indicates that the element is explicitly closed using the /name method.
        ''' </summary>
        Property IsExplicitlyTerminated As Boolean
            Get
                Return mIsExplicitlyTerminated
            End Get
            Set
                mIsTerminated = Not Value
                mIsExplicitlyTerminated = Value
            End Set
        End Property

        Friend ReadOnly Property NoEscaping() As Boolean
            Get
                Return "script".Equals(Name.ToLower()) OrElse "style".Equals(Name.ToLower())
            End Get
        End Property

        ''' <summary>
        ''' This will return the HTML representation of this element.
        ''' </summary>
        ''' <returns></returns>
        Public Overrides Function ToString() As String
            Return HTML
        End Function

        ''' <summary>
        ''' This will return the HTML representation of this element.
        ''' </summary>
        ''' <returns></returns>
        <Category("General"), Description("The element representation (tag with attributes)")>
        Public Overrides ReadOnly Property ElementRepresentation As String
            Get
                Dim value As String = Convert.ToString("<") & mName
                For Each attribute As HtmlAttribute In Attributes
                    value += " " + attribute.ToString()
                Next
                value += ">"
                Return value
            End Get

        End Property

        <Category("Output"), Description("A concatination of all the text associated with this element")>
        Public Property InnerText As String
            Get
                Dim stringBuilder As New StringBuilder()
                For Each node As HtmlNode In Nodes
                    If TypeOf node Is HtmlText Then
                        stringBuilder.Append(DirectCast(node, HtmlText).Text)
                    Else
                        If Not Nodes.mParent.Name.IsIn({"script", "style", "head"}) AndAlso DirectCast(node, HtmlElement).Name.IsIn({"script", "style", "head"}) Then
                            stringBuilder.Append(DirectCast(node, HtmlElement).InnerText)
                        End If
                    End If
                Next
                Return stringBuilder.ToString()
            End Get
            Set(value As String)
                Me.InnerHTML = value.RemoveHTML
            End Set
        End Property

        ''' <summary>
        ''' Return a html string of child nodes
        ''' </summary>
        ''' <returns></returns>
        '''
        <Category("Output"), Description("The string representation of all childnodes")>
        Public Property InnerHTML As String
            Get
                Dim s = ""
                For Each node As HtmlNode In Nodes
                    If TypeOf node Is HtmlElement Then
                        s.Append(node.HTML)
                    Else
                        s.Append(CType(node, HtmlText).Text)
                    End If
                Next
                Return s
            End Get
            Set(value As String)
                Dim d As New HtmlDocument(value)
                Me.Nodes.Clear()
                For Each n As HtmlNode In d.Nodes
                    n.mParent = Me
                    Me.Nodes.Add(n)
                Next
            End Set
        End Property

        ''' <summary>
        ''' This will return the HTML for this element and all subnodes.
        ''' </summary>
        <Category("Output"), Description("The HTML string representation of this element and all childnodes")>
        Public Overrides ReadOnly Property HTML() As String
            Get
                Dim shtml As New StringBuilder()
                shtml.Append(Convert.ToString("<") & mName)
                For Each attribute As HtmlAttribute In Attributes
                    shtml.Append(" " + attribute.HTML)
                Next
                If Nodes.Count > 0 Then
                    shtml.Append(">")
                    For Each node As HtmlNode In Nodes
                        shtml.Append(node.HTML)
                    Next
                    shtml.Append((Convert.ToString("</") & mName) + ">")
                Else
                    If IsExplicitlyTerminated Then
                        shtml.Append((Convert.ToString("></") & mName) + ">")
                    ElseIf IsTerminated Then
                        shtml.Append("/>")
                    Else
                        shtml.Append(">")
                    End If
                End If
                Return shtml.ToString()
            End Get
        End Property

        ''' <summary>
        ''' This will return the XHTML for this element and all subnodes.
        ''' </summary>
        <Category("Output"), Description("The XHTML string representation of this element and all childnodes")>
        Public Overrides ReadOnly Property XHTML() As String
            Get
                If "html".Equals(mName) AndAlso Me.Attributes("xmlns") Is Nothing Then
                    Attributes.Add(New HtmlAttribute("xmlns", "http://www.w3.org/1999/xhtml"))
                End If
                Dim html As New StringBuilder()
                html.Append("<" + mName.ToLower())
                For Each attribute As HtmlAttribute In Attributes
                    html.Append(" " + attribute.XHTML)
                Next
                If IsTerminated Then
                    html.Append("/>")
                Else
                    If Nodes.Count > 0 Then
                        html.Append(">")
                        For Each node As HtmlNode In Nodes
                            html.Append(node.XHTML)
                        Next
                        html.Append("</" + mName.ToLower() + ">")
                    Else
                        html.Append("/>")
                    End If
                End If
                Return html.ToString()
            End Get
        End Property

        ''' <summary>
        ''' Return the <see cref="XmlElement"/> equivalent to this node
        ''' </summary>
        ''' <returns></returns>
        Function ToXmlElement() As XmlElement
            Dim doc As New XmlDocument
            doc.LoadXml(XHTML)
            Return doc.DocumentElement
        End Function

        ''' <summary>
        ''' This will search though this collection of nodes for all elements with matchs the predicate.
        ''' </summary>
        ''' <typeparam name="NodeType">Type of Node (<see cref="HtmlElement"/> or <see cref="HtmlText"/>)</typeparam>
        ''' <param name="predicate">The predicate to match the nodes</param>
        ''' <param name="SearchChildren">Travesse the child nodes</param>
        ''' <returns></returns>
        Public Function FindElements(Of NodeType As HtmlNode)(predicate As Func(Of NodeType, Boolean), Optional SearchChildren As Boolean = True) As HtmlNodeCollection
            Return Me.Nodes.FindElements(Of NodeType)(predicate, SearchChildren)
        End Function

    End Class

    Public Class HtmlInput
        Inherits HtmlElement

        Enum HtmlInputType
            text
            button
            checkbox
            color
            [date]
            datetime_local
            email
            file
            hidden
            image
            month
            number
            password
            radio
            range
            reset
            search
            submit
            tel
            time
            url
            week
        End Enum

        Sub New(Type As HtmlInputType, Optional Value As Object = Nothing)
            MyBase.New("input")
            mIsExplicitlyTerminated = True
            Me.Value = Value
            Me.Type = Type
        End Sub

        ''' <summary>
        ''' Type of Input
        ''' </summary>
        ''' <returns></returns>
        Property Type As HtmlInputType
            Get
                Return GetEnumValue(Of HtmlInputType)(Me.Attribute("type"))
            End Get
            Set(value As HtmlInputType)
                Me.Attribute("type") = [Enum].GetName(GetType(HtmlInputType), value)
            End Set
        End Property

        ''' <summary>
        ''' Value of Input
        ''' </summary>
        ''' <returns></returns>
        Property Value As Object
            Get
                Return Me.Attribute("value")
            End Get
            Set(value As Object)
                Me.Attribute("value") = ("" & value)
            End Set
        End Property

    End Class

    Public Class HtmlSelectElement
        Inherits HtmlElement

        ''' <summary>
        ''' Returns the name of element (OL or UL)
        ''' </summary>
        ''' <returns></returns>
        Shadows Property Name As String
            Get
                Return "select"
            End Get
            Set(value As String)
                MyBase.Name = "select"
            End Set
        End Property

        ''' <summary>
        ''' Create a select element
        ''' </summary>
        Sub New()
            MyBase.New("select")
        End Sub

        ''' <summary>
        ''' Add a option to this list
        ''' </summary>
        ''' <param name="Option"></param>
        Public Sub AddOption([Option] As HtmlOptionElement)
            Me.Nodes.Add([Option])
        End Sub

        Public ReadOnly Property Groups As IEnumerable(Of String)
            Get
                Return Me("option").Select(Function(a As HtmlOptionElement) a.Group).Distinct
            End Get
        End Property

        ''' <summary>
        ''' Redefines the node elements
        ''' </summary>
        Public Sub Organize()
            If Groups.Count > 0 Then
                Dim opts = Me("option")
                For Each group In Groups
                    Dim d As New HtmlElement("optgroup")
                    d.IsExplicitlyTerminated = True
                    d.Attribute("label") = group
                    Me.Nodes.Add(d)
                Next
                For Each opt In Me("option")
                    Dim o = CType(opt, HtmlOptionElement)
                    If o.Group.IsNotBlank Then
                        Dim destination = Me("optgroup[label=" & CType(opt, HtmlOptionElement).Group.Quote & "]").First
                        o.Move(destination)
                    Else
                        o.Move(Me)
                    End If
                Next
            End If
        End Sub

    End Class

    Public Class HtmlOptionElement
        Inherits HtmlElement

        Sub New()
            MyBase.New("option")
        End Sub

        Sub New(Text As String)
            MyBase.New("option", Text.RemoveHTML)
        End Sub

        Sub New(Text As String, Value As String)
            MyBase.New("option", Text.RemoveHTML)
            Me.Attribute("value") = Value
        End Sub

        Property Group As String = ""

    End Class

    Public Class HtmlListElement
        Inherits HtmlElement

        Property IsOrdenedList As Boolean

        ''' <summary>
        ''' Returns the name of element (OL or UL)
        ''' </summary>
        ''' <returns></returns>
        Shadows Property Name As String
            Get
                Return MyBase.Name
            End Get
            Set(value As String)
                MyBase.Name = If(IsOrdenedList, "ol", "ul")
            End Set
        End Property

        ''' <summary>
        ''' Create a List element (OL or UL)
        ''' </summary>
        ''' <param name="OrdenedList"></param>
        Sub New(Optional OrdenedList As Boolean = False)
            MyBase.New(If(OrdenedList, "ol", "ul"))
            IsOrdenedList = OrdenedList
        End Sub

        ''' <summary>
        ''' Add a LI to this list
        ''' </summary>
        ''' <param name="Text"></param>
        Public Sub Add(Text As String)
            Me.Nodes.Add(New HtmlElement("li", Text) With {.IsExplicitlyTerminated = True})
        End Sub

        ''' <summary>
        ''' Add a LI to this list
        ''' </summary>
        ''' <param name="Content"></param>
        Public Sub Add(ParamArray Content As HtmlNode())
            Dim d = New HtmlElement("li")
            d.IsExplicitlyTerminated = True
            For Each i In Content
                i.Move(d)
            Next
            Me.Nodes.Add(d)
        End Sub

    End Class

End Namespace

'=======================================================
'Service provided by Telerik (www.telerik.com)
'Conversion powered by NRefactory.
'Twitter: @telerik
'Facebook: facebook.com/telerik
'=======================================================