Imports System.IO

Friend Class frm_Gruppen
    ' Path-group editor used by Findit6Agent to define named search spaces.
    ' A search space can combine directories from different local, removable or network drives.
    ' The TreeView is populated lazily because accessing large or unavailable drives may be expensive.
    ' This form is a reduced derivative of Findit6's multi-purpose directory TreeView; edit TreeView logic with care.
    ' Path groups are kept in memory while editing and persisted to the configured Pathgroup INI when confirmed.

    ' TreeView and interaction state.
    Private bDontWork As Boolean ' Prevents recursive checkbox updates while :SUB handling changes node state.
    Private bSubs As Boolean ' Determines whether the current click should include subdirectories (:SUB).
    Private bCollapse As Boolean ' Requests immediate collapse after an unavailable drive was expanded.
    Friend bGroupload As Boolean = False ' True while a stored group is being loaded into the TreeView.
    Friend bIgnoreBlocks As Boolean
    ' Was a new group created?
    Friend bNewGroup As Boolean = False
    ' Path-group data currently edited by the form.
    Public aPathGroupList As New List(Of PathGroup)
    Public arrAktiveGroup As New PathGroup
    Private iGroupSelectedIndex As Integer
    Private bNodeIsChecked As Boolean ' Remembers the node checkbox state before the current mouse click.
    Private bUserIsClicking As Boolean ' Distinguishes initial TreeView setup from later user-triggered expansion.
    Public sNewGRPname As String = ""
    Friend bCheckGroupComplete As Boolean = True ' Controls whether empty path groups are rejected.

#Region "Form initialization and closing"
    ' Initializes the form and its designer-created controls.
    Public Sub New()
        InitializeComponent()
    End Sub

    ' Initializes lettering, colors, the directory tree and the stored path groups when the form opens.
    ' If no group exists yet, the user is immediately asked to create the first search space.
    Private Sub frm_Load(sender As Object, e As EventArgs) Handles Me.Load
        Me.Visible = False
        frmMain.Cursor = Cursors.WaitCursor
        ' Apply localized captions and the current application color scheme.
        Me.Text = LetteringIni.getLettering("frm_Treepfad", "Me")
        lblBeschreibung.Text = LetteringIni.getLettering("frm_Treepfad", "lblBeschreibung")
        bRename.Text = LetteringIni.getLettering("frm_Treepfad", "bRename")
        bCancel.Text = LetteringIni.getLettering("General", "bEsc")
        bOK.Text = LetteringIni.getLettering("General", "bOK")
        ToolStripMenuItem1.Text = LetteringIni.getLettering("frm_Treepfad", "Neuordner")
        cmbGruppen.BackColor = clr.Buttons
        cmbGruppen.ForeColor = clr.Text
        TxtbxBeschreibung.BackColor = clr.Buttons
        TxtbxBeschreibung.ForeColor = clr.Text
        ToolStripMenuItem1.BackColor = clr.Text
        ToolStripMenuItem1.ForeColor = clr.Text
        lblGruppe.ForeColor = clr.Text
        lblBeschreibung.ForeColor = clr.Text
        lblBeschreibung.Visible = True
        lblBeschreibung.BackColor = clr.Background
        lblBeschreibung.BringToFront()
        lblPfade.ForeColor = clr.Text
        TreeView1.BackColor = clr.Buttons
        TreeView1.ForeColor = clr.Text
        Me.BackColor = clr.Background
        bNeu.BackColor = clr.Buttons
        bNeu.ForeColor = clr.Text
        bDel.BackColor = clr.Buttons
        bDel.ForeColor = clr.Text
        bOK.BackColor = clr.Buttons
        bOK.ForeColor = clr.Text
        bRename.BackColor = clr.Buttons
        bRename.ForeColor = clr.Text
        bCancel.ForeColor = bOK.ForeColor
        bCancel.BackColor = bOK.BackColor
        ' Build the directory tree and restore the saved path groups.
        TreeView1.BeginUpdate()
        TreeView1.Nodes.Clear()
        Dim bGroupsExist As Boolean = s_GruppenLaden()
        TxtbxBeschreibung.Text = arrAktiveGroup.Description
        TreeView1.EndUpdate()
        frmMain.Cursor = Cursors.Default
        System.Threading.Thread.Sleep(100)
        ' Position and activate the editor after the initial TreeView build.
        Me.Location = New Point(frmMain.Left + 100 + 300, frmMain.Top + 50)
        Me.Owner = frmMain
        Me.KeyPreview = True
        Me.Validate()
        cmbGruppen.Focus()
        ' A completely new installation starts by creating its first path group.
        If Not bGroupsExist Then
            bCheckGroupComplete = False
            bNewGroup = True
            frmNaming.bNew = True
            frmNaming.ShowDialog(Me)
            If sNewGRPname = "" Then
                Me.Close()
                Exit Sub
            End If
            Dim pg As New PathGroup
            aPathGroupList.Add(pg)
            cmbGruppen.Items.Add("")
            Dim newIndex As Integer = cmbGruppen.Items.Count - 1
            cmbGruppen.SelectedIndex = newIndex
            cmbGruppen.Items(newIndex) = sNewGRPname
            aPathGroupList(newIndex).Name = sNewGRPname
            sNewGRPname = ""
            bCheckGroupComplete = True
        Else
            For i As Integer = 0 To cmbGruppen.Items.Count - 1
                If cmbGruppen.Items(i).ToString() = frmMain.txtWhere.Text Then
                    cmbGruppen.SelectedIndex = i
                    Exit For
                End If
            Next
        End If
        SetDarkTitleBar(Me)
        Me.Visible = True
    End Sub

    ' Loads the stored path groups and prepares the form for group editing.
    ' Builds the directory tree, fills the group selector and displays the first available group.
    ' Returns True when at least one stored path group exists.
    Private Function s_GruppenLaden() As Boolean
        Dim bGroupsExist As Boolean = False
        ' Configure the form for path-group editing and build the directory roots.
        bGroupload = True
        TreeView1.CheckBoxes = True
        lblGruppe.Visible = True
        lblPfade.Visible = True
        cmbGruppen.Visible = True
        bNeu.Text = LetteringIni.getLettering("frm_Treepfad", "bNeu")
        bDel.Text = LetteringIni.getLettering("frm_Treepfad", "bDel")
        lblPfade.Text = LetteringIni.getLettering("frm_Treepfad", "lblpfade")
        lblGruppe.Text = LetteringIni.getLettering("frm_Treepfad", "lblGruppe")
        s_TREEfüllen()
        cmbGruppen.Items.Clear()
        ' Load all groups from the configured INI and expose their names in the selector.
        LoadPathGroupList()
        If aPathGroupList.Count = 0 Then
            bGroupsExist = False
        Else
            For Each group As PathGroup In aPathGroupList
                cmbGruppen.Items.Add(group.Name)
            Next
            bGroupsExist = True
        End If
        bNeu.Visible = True
        bDel.Visible = True
        ' Display the first available group and restore its checked directory nodes.
        If bGroupsExist Then
            cmbGruppen.SelectedIndex = 0
            iGroupSelectedIndex = 0
            arrAktiveGroup = aPathGroupList(0)
            TreeView1.Focus()
            lblGruppe.Text = LetteringIni.getLettering("frm_Treepfad", "lblGruppe")
            GruppenpfadeInTree()
            TreeView1.SelectedNode = Nothing
        End If
        bGroupload = False
        Return bGroupsExist
    End Function

    ' Returns focus to the main window when the path-group editor closes.
    Private Sub frm_TreePfad_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        frmMain.BringToFront()
    End Sub

    ' Returns focus to the main window after the form has been disposed.
    Private Sub frm_TreePfad_Disposed(sender As Object, e As EventArgs) Handles Me.Disposed
        frmMain.BringToFront()
    End Sub

#End Region

#Region "Path group management"
    ' Displays the paths of the currently active group in the TreeView.
    ' Existing checks are cleared first; each stored path is then located recursively and marked.
    Sub GruppenpfadeInTree()
        bUserIsClicking = False
        DeselectNodes(TreeView1.Nodes)
        TreeView1.CollapseAll()
        Dim spfad As String = ""
        For i As Integer = 0 To arrAktiveGroup.PathCount - 1
            spfad = arrAktiveGroup.Paths(i)
            s_CHECKnode(spfad, TreeView1.Nodes)
        Next
        bUserIsClicking = True
    End Sub

    ' Recursively locates a stored group path in the TreeView and restores its checked/:SUB state.
    ' Parent nodes are expanded only as needed so deeply nested paths can be reached.
    Private Sub s_CHECKnode(ByVal sGruppenPfad As String, ByVal Nodes As TreeNodeCollection)
        Dim sGruppenPfadPur As String = "" ' Stored path without the :SUB marker.
        If sGruppenPfad.EndsWith(":SUB") Then
            sGruppenPfadPur = sGruppenPfad.Substring(0, sGruppenPfad.Length - 4)
        Else
            sGruppenPfadPur = sGruppenPfad
        End If
        On Error Resume Next
        For Each lNode As TreeNode In Nodes
            If lNode.Name = "FAV" Then Continue For
            If lNode.Text = "netdrive!" Then Continue For
            If lNode.Name.ToLower = sGruppenPfadPur.Trim.ToLower Then
                lNode.Checked = True
                If sGruppenPfad.EndsWith(":SUB") Then
                    lNode.Text &= " +SUB"
                    Exit Sub
                Else
                    lNode.Expand()
                    lNode.Tag = "+"
                End If
            End If
            If InStr(sGruppenPfadPur.ToLower, lNode.Name.ToLower) <> 0 Or lNode.Name.Substring(1, 1) <> ":" Then
                If lNode.Tag.ToString <> "+" Then
                    lNode.Tag = "+"
                    lNode.Expand()
                End If
                s_CHECKnode(sGruppenPfad, lNode.Nodes)
            End If
        Next
    End Sub

    ' Removes a path from the active group using a case-insensitive comparison.
    ' Iteration runs backwards so matching entries can be removed safely.
    Public Sub DelPathFromGroup(ByVal sPathToDel As String)
        If String.IsNullOrWhiteSpace(sPathToDel) Then
            Exit Sub
        End If
        For i As Integer = arrAktiveGroup.Paths.Count - 1 To 0 Step -1
            If String.Compare(arrAktiveGroup.Paths(i), sPathToDel, StringComparison.OrdinalIgnoreCase) = 0 Then
                arrAktiveGroup.Paths.RemoveAt(i)
            End If
        Next
        arrAktiveGroup.PathCount = arrAktiveGroup.Paths.Count
    End Sub

    ' Adds a selected path to the active group and updates its stored path count.
    Public Sub ADDPathTOGroup(ByVal sPathToADD As String)
        If String.IsNullOrWhiteSpace(sPathToADD) Then
            Exit Sub
        End If
        arrAktiveGroup.PathCount += 1
        arrAktiveGroup.Paths.Add(sPathToADD)
    End Sub

    ' Removes child paths that are already covered by another selected parent path with :SUB.
    ' This keeps the path group compact and avoids redundant directory searches.
    Private Sub RemoveRedundantPaths()
        Dim sOrgPathAsStored As String = ""
        Dim pathsWithSub As New List(Of String)
        For Each path As String In arrAktiveGroup.Paths
            If path.EndsWith(":SUB") Then
                pathsWithSub.Add(path.Substring(0, path.Length - 4).Trim.ToLower)
            End If
        Next
        Dim toRemove As New List(Of String)
        For Each PotentialParent As String In pathsWithSub
            For Each path As String In arrAktiveGroup.Paths
                sOrgPathAsStored = path
                If path.EndsWith(":SUB") Then path = path.Substring(0, path.Length - 4).Trim
                path = path.ToLower
                If PotentialParent = path Then Continue For
                If path.StartsWith(PotentialParent & "\") OrElse path.StartsWith(PotentialParent & "/") Then
                    toRemove.Add(sOrgPathAsStored)
                End If
            Next
        Next
        For Each path As String In toRemove
            arrAktiveGroup.Paths.Remove(path)
        Next
        arrAktiveGroup.PathCount = arrAktiveGroup.Paths.Count
    End Sub

    ' Reads all saved path groups from PathgroupIni into the in-memory group list.
    ' Each group is reconstructed from its metadata and numbered Path1..PathN entries.
    Public Sub LoadPathGroupList()
        aPathGroupList.Clear()
        If PathgroupIni Is Nothing Then
            Exit Sub
        End If
        Dim numberOfGroups As Integer = CInt(PathgroupIni.ReadValue("Main", "NumbersofGroups", "0"))
        If numberOfGroups = 0 Then
            Exit Sub
        End If
        For i As Integer = 1 To numberOfGroups
            Dim section As String = i.ToString()
            Dim group As New PathGroup With {.Mode = CInt(PathgroupIni.ReadValue(section, "Mode", "1")), .Name = PathgroupIni.ReadValue(section, "Name", ""), .Description = PathgroupIni.ReadValue(section, "Description", ""), .PathCount = CInt(PathgroupIni.ReadValue(section, "PathCount", "0"))}
            For j As Integer = 1 To group.PathCount
                Dim pathKey As String = "Path" & j.ToString()
                Dim pathValue As String = PathgroupIni.ReadValue(section, pathKey, "")
                If pathValue <> "" Then
                    group.Paths.Add(pathValue)
                End If
            Next
            aPathGroupList.Add(group)
        Next
    End Sub

    ' Rewrites the complete path-group INI from the current in-memory list.
    ' Empty groups are skipped and the remaining groups are renumbered consecutively.
    Public Sub SavePathGroupInI()

        Dim validGroups As New List(Of PathGroup)

        For Each group As PathGroup In aPathGroupList
            If Not String.IsNullOrWhiteSpace(group.Name) AndAlso group.Paths.Count > 0 Then
                validGroups.Add(group)
            End If
        Next

        Dim sb As New System.Text.StringBuilder()

        sb.AppendLine("[Main]")
        sb.AppendLine("NumbersofGroups=" & validGroups.Count.ToString())
        sb.AppendLine()

        For i As Integer = 0 To validGroups.Count - 1

            Dim group As PathGroup = validGroups(i)
            Dim section As String = (i + 1).ToString()

            sb.AppendLine("[" & section & "]")
            sb.AppendLine("Mode=1")
            sb.AppendLine("Name=" & group.Name)
            sb.AppendLine("Description=" & group.Description)
            sb.AppendLine("PathCount=" & group.Paths.Count.ToString())

            For j As Integer = 0 To group.Paths.Count - 1
                sb.AppendLine("Path" & (j + 1).ToString() & "=" & group.Paths(j))
            Next

            If i < validGroups.Count - 1 Then sb.AppendLine()

        Next

        File.WriteAllText(sPathgroupIni, sb.ToString(), New System.Text.UTF8Encoding(True))


    End Sub

    ' Recursively clears TreeNode.Tag values used to track lazy directory expansion.
    Private Sub ClearAllTags(node As TreeNode)
        If node Is Nothing Then Return
        node.Tag = Nothing
        For Each child As TreeNode In node.Nodes
            ClearAllTags(child)
        Next
    End Sub

#End Region

#Region "Directory TreeView"
    ' Builds the initial directory TreeView from Windows special folders and logical drives.
    ' Only the first child is added where possible; deeper directories are loaded lazily when expanded.
    Private Sub s_TREEfüllen()
        Dim N As TreeNode
        Dim n1 As TreeNode
        Dim sName As String = ""
        On Error Resume Next
        TreeView1.BeginUpdate()
        ' Add common Windows folders as convenient favorites.
        sName = LetteringIni.getLettering("frm_Treepfad", "fav")
        n1 = TreeView1.Nodes.Add("FAV", sName, 8, 8)
        Dim Docs As String = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        N = n1.Nodes.Add(Docs, LetteringIni.getLettering("frm_Treepfad", "desk"), 9, 9)
        N.Nodes.Add("dummy", "dummy", 1, 1)
        Docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        N = n1.Nodes.Add(Docs, LetteringIni.getLettering("frm_Treepfad", "doks"), 10, 10)
        N.Nodes.Add("dummy", "dummy", 1, 1)
        Docs = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        N = n1.Nodes.Add(Docs, LetteringIni.getLettering("frm_Treepfad", "pic"), 11, 11)
        N.Nodes.Add("dummy", "dummy", 1, 1)
        Docs = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        N = n1.Nodes.Add(Docs, LetteringIni.getLettering("frm_Treepfad", "mus"), 12, 12)
        N.Nodes.Add("dummy", "dumy", 1, 1)
        ' OneDrive is inferred from the standard user profile location and only added if it exists.
        Docs = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Docs = Docs.Replace("AppData\Roaming", "Onedrive")
        If IO.Directory.Exists(Docs) Then
            N = n1.Nodes.Add(Docs, "OneDrive", 15, 15)
            N.Nodes.Add("dummy", "dummy", 1, 1)
        End If
        ' Add all logical drives. Network/removable drives receive placeholders and are tested only on demand.
        sName = LetteringIni.getLettering("frm_Treepfad", "com")
        n1 = TreeView1.Nodes.Add(sName, sName, 13, 13)
        For Each Drive As String In Environment.GetLogicalDrives
            Dim LW As New DriveInfo(Drive)
            Dim DriveType As Integer = CInt(LW.DriveType)
            If String.Equals(Path.GetPathRoot(Drive), Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase) Then DriveType = 7
            If DriveType = 4 OrElse DriveType = 5 Then
                N = n1.Nodes.Add(Drive, Mid(Drive, 1, 3), DriveType, DriveType)
                If DriveType = 4 Then
                    N.Nodes.Add(Drive, "netdrive!", 1, 1)
                Else
                    N.Nodes.Add(Drive, "removable!", 1, 1)
                End If
            Else
                If LW.IsReady Then
                    N = n1.Nodes.Add(Drive, LW.VolumeLabel & " (" & Mid(Drive, 1, 2) & ")", DriveType, DriveType)
                    For Each Verzeichnis As IO.DirectoryInfo In New IO.DirectoryInfo(Drive).GetDirectories()
                        N.Nodes.Add(Verzeichnis.FullName, Verzeichnis.Name, 1, 1)
                        Exit For
                    Next
                End If
            End If
        Next
        TreeView1.EndUpdate()
    End Sub

    Private Function f_CHECKblocker(VZ As String) As Boolean
        If bIgnoreBlocks Then Return False
        If arrBlockListe.Count = 0 Then Return False

        Dim icount As Integer = arrBlockListe.Count
        Dim stest As String = VZ.ToString.ToLower
        If stest.Substring(stest.Length - 1) = "\" Then stest = stest.Substring(0, stest.Length - 1)

        On Error Resume Next

        If arrBlockListe(0).ToString = "ex" Then
            For x As Integer = 1 To icount - 1
                If arrBlockListe(x).ToString = stest Then Return True
            Next
        ElseIf arrBlockListe(0).ToString = "in" Then
            For i As Integer = 1 To arrBlockListe.Count - 1
                Dim stest2 As Integer = stest.IndexOf(arrBlockListe(i).ToString.ToLower)
                Dim stest3 As Integer = arrBlockListe(i).ToString.ToLower.IndexOf(stest)
                If stest2 > -1 OrElse stest3 > -1 Then Return False
            Next
            Return True
        End If

        Return False
    End Function

    Private Function f_CHECKbUserblock(VZ As String) As Boolean
        If bIgnoreBlocks Then Return False
        If arrIgnore.Count = 0 Then Return False

        Dim icount As Integer = arrIgnore.Count
        Dim stest As String = VZ.ToString.ToLower
        If stest.Substring(stest.Length - 1) = "\" Then stest = stest.Substring(0, stest.Length - 1)

        On Error Resume Next

        For x As Integer = 1 To icount - 1
            If arrIgnore(x).ToString = stest Then Return True
        Next

        Return False
    End Function


    ' Lazily loads the direct subdirectories of an expanded TreeView node.
    ' Hidden or inaccessible folders are skipped/marked and one child placeholder is added for expandable nodes.
    Private Sub s_DIRSeinlesen(ByVal Knoten As TreeNode)
        ' Do not enumerate the same user-expanded node repeatedly.
        Try
            If bUserIsClicking AndAlso Knoten.Tag.ToString = "+" Then Exit Sub
        Catch
        End Try
        Dim iIconNr As Integer = 1
        Dim Verzeichnis As New IO.DirectoryInfo(CType(Knoten.Name.Replace(" :SUB", ""), String))
        If Not Verzeichnis.Exists Then Exit Sub
        Dim SubVZ As IO.DirectoryInfo : Dim N As TreeNode
        ' Replace the placeholder node with the real directory contents.
        Try
            TreeView1.BeginUpdate() ' Avoid flicker while directory nodes are added.
            Knoten.Nodes.Clear()
            For Each SubVZ In Verzeichnis.EnumerateDirectories().OrderBy(Function(d) d.Name, StringComparer.OrdinalIgnoreCase)
                Try
                    If (File.GetAttributes(SubVZ.FullName) And FileAttributes.Hidden) = FileAttributes.Hidden Then Continue For
                Catch ex As Exception
                End Try
                Try
                    Try
                        For Each SubSubVZ As IO.DirectoryInfo In New IO.DirectoryInfo(SubVZ.FullName).EnumerateDirectories
                            iIconNr = 1
                            Exit For
                        Next
                    Catch ex As Exception
                        If SubVZ.Name = "OneDrive" Then
                            iIconNr = 1
                        Else
                            iIconNr = 14
                        End If
                    End Try
                    N = Knoten.Nodes.Add(SubVZ.FullName, SubVZ.Name, iIconNr, 1)
                    ' Add one child as an expansion marker instead of enumerating the complete subtree now.
                    If iIconNr = 1 Then
                        Try
                            For Each SubSubVZ As IO.DirectoryInfo In New IO.DirectoryInfo(SubVZ.FullName).GetDirectories
                                N.Nodes.Add(SubSubVZ.FullName, SubSubVZ.Name, 14, 1)
                                Exit For
                            Next
                        Catch ex As Exception
                            LogDebug("s_DIRSeinlesen - Auf vorhandene UVZ checken " & ex.Message)
                        End Try
                    End If
                Catch ex As Exception
                    LogDebug("- s_DIRSeinlesen -Gefundenes hinzufügen " & ex.Message)
                End Try
            Next
            TreeView1.EndUpdate()
        Catch ex As Exception
            LogDebug("- s_DIRSeinlesen - Hauptscheife " & ex.Message)
        End Try
    End Sub

    ' Restores the normal cursor after expansion and collapses a node again if its drive proved unavailable.
    Private Sub TreeView1_AfterExpand(sender As Object, e As TreeViewEventArgs) Handles TreeView1.AfterExpand
        Cursor = Cursors.Default
        If bCollapse Then e.Node.Collapse() : bCollapse = False
    End Sub

    ' Prepares a node before expansion, including availability checks for removable/network drives.
    ' The actual subdirectory list is then loaded on demand by s_DIRSeinlesen().
    Private Sub TreeView1_BeforeExpand(sender As Object, e As TreeViewCancelEventArgs) Handles TreeView1.BeforeExpand
        ' Loads subdirectories when a node is expanded.
        ' Removable, network and CD-ROM drives are checked for availability first.
        Cursor = Cursors.WaitCursor
        Dim N As TreeNode = e.Node
        If "245".Contains(e.Node.ImageIndex.ToString) Then
            Dim sTextmerker As String = e.Node.Text
            If Not s_LWcheckReady(e.Node) Then
                If e.Node.ImageIndex = 4 Then
                    If bGroupload Then
                        Me.Visible = True
                        MsgBoxNeu(LetteringIni.getLettering("frm_Treepfad", "notreadyforgroup").Replace("%DRIVE%", e.Node.Name), MsgBoxStyle.OkOnly)
                    Else
                        MsgBoxNeu(LetteringIni.getLettering("frm_Treepfad", "notReady"), MsgBoxStyle.OkOnly)
                    End If
                Else
                    MsgBoxNeu(LetteringIni.getLettering("frm_Treepfad", "insdisc"), MsgBoxStyle.OkOnly)
                End If
                e.Node.Text = sTextmerker.Replace(" +SUB", "")
                e.Node.Checked = False
                N.Collapse()
                Cursor = Cursors.Default
                Exit Sub
            End If

            e.Node.Text = sTextmerker
        End If
        s_DIRSeinlesen(N)
        Cursor = Cursors.Default

        If e.Node.ImageIndex <> 2 AndAlso e.Node.ImageIndex <> 4 AndAlso e.Node.ImageIndex <> 5 Then e.Node.Tag = "+"
    End Sub

    ' Recursively resets TreeView selections and removes displayed/stored :SUB markers.
    Private Sub DeselectNodes(ByVal nodes As TreeNodeCollection)
        For Each node As TreeNode In nodes
            node.Checked = False ' Clear the visual selection.
            If node.Text.EndsWith("+SUB") Then node.Text = node.Text.Substring(0, node.Text.Length - 4).Trim
            If node.Name.EndsWith(":SUB") Then node.Name = node.Name.Substring(0, node.Name.Length - 4).Trim
            If node.Nodes.Count > 0 Then
                DeselectNodes(node.Nodes)
            End If
        Next
    End Sub

    ' Checks whether a drive represented by a TreeView node is currently available.
    ' Updates its display text when successful and requests immediate collapse when access fails.
    Private Function s_LWcheckReady(ByRef node As TreeNode) As Boolean
        Dim sMerker As String = node.Text
        If node.ImageIndex = 4 Then node.Text = LetteringIni.getLettering("frm_Treepfad", "nettest2")
        Cursor = Cursors.WaitCursor
        Try
            Dim LW As New DriveInfo(node.Name)
            TreeView1.Refresh()
            If LW.IsReady Then
                If Not sMerker.Contains(LW.VolumeLabel) Then
                    node.Text = LW.VolumeLabel & " (" & sMerker & ")"
                Else
                    node.Text = sMerker
                End If
                node.Name = LW.Name
                node.Tag = ""
                Cursor = Cursors.Default
                Return True
            End If
        Catch
        End Try
        Cursor = Cursors.Default
        node.Text = sMerker
        bCollapse = True
        Return False
    End Function

#End Region

#Region "TreeView interaction"
    ' Updates visual state after a checkbox change and applies the temporary :SUB marker when required.
    ' Guard flags prevent checkbox handling from recursively triggering itself.
    Private Sub TreeView1_AfterCheck(sender As Object, e As TreeViewEventArgs) Handles TreeView1.AfterCheck
        If Not e.Node.FullPath.Contains("\") Then Exit Sub
        Cursor = Cursors.WaitCursor
        If e.Node.Checked Then
            e.Node.ForeColor = clr.lightgreen
        Else
            e.Node.ForeColor = clr.Text
            e.Node.Name = e.Node.Name.Replace(" :SUB", "")
        End If
        If bDontWork OrElse Not bSubs Then Cursor = Cursors.Default : Exit Sub
        bDontWork = True
        e.Node.Name = e.Node.Name.Replace(" :SUB", "") & " :SUB"
        On Error Resume Next
        Cursor = Cursors.Default
        bDontWork = False
    End Sub

    ' Handles a click on a directory node and synchronizes the active PathGroup with its new checked state.
    ' Drive availability is verified first; left/right click semantics determine whether :SUB is stored.
    Private Sub nodeClick(sender As Object, e As TreeNodeMouseClickEventArgs) Handles TreeView1.NodeMouseClick
        If e.Node.ImageIndex = 14 Then
            Dim ii As Integer = MsgBoxNeu(LetteringIni.getLettering("frm_Treepfad", "forbidden"), MsgBoxStyle.OkOnly)
            TreeView1.SelectedNode = Nothing
            e.Node.Checked = False
            TreeView1.SelectedNode = Nothing
            Exit Sub
        End If
        ' Verify removable/network drives before allowing selection.
        If "245".Contains(e.Node.ImageIndex.ToString) Then
            Dim sTextmerker As String = e.Node.Text
            If Not s_LWcheckReady(e.Node) Then
                e.Node.Checked = False : e.Node.Collapse()
                On Error Resume Next ' The :SUB display marker may already be absent.
                e.Node.Text = sTextmerker.Replace(" +SUB", "")
                If e.Node.ImageIndex = 4 Then
                    Dim ii As Integer = MsgBoxNeu(LetteringIni.getLettering("frm_Treepfad", "notReady"), MsgBoxStyle.OkOnly)
                Else
                    Dim ii As Integer = MsgBoxNeu(LetteringIni.getLettering("frm_Treepfad", "insdisc"), MsgBoxStyle.OkOnly)
                End If
                Exit Sub
            End If
            e.Node.Text = sTextmerker
        End If
        If e.Node.ImageIndex = 14 OrElse e.Node.Name.Replace(" :SUB", "") = "FAV" OrElse e.Node.Name.Replace(" :SUB", "") = "Computer" Then
            e.Node.Checked = False
            e.Node.Expand()
            TreeView1.SelectedNode = e.Node.Nodes(0)
            Exit Sub
        End If
        ' Synchronize the clicked node with the active PathGroup.
        If bNodeIsChecked <> e.Node.Checked Then
            If bNodeIsChecked Then
                If e.Node.Text.EndsWith("+SUB") Then e.Node.Text = e.Node.Text.Substring(0, e.Node.Text.Length - 4).Trim
                DelPathFromGroup(e.Node.Name)
            Else
                If e.Node.Name.EndsWith(":SUB") Then e.Node.Name = e.Node.Name.Substring(0, e.Node.Name.Length - 4).Trim
                If bSubs Then
                    e.Node.Text &= " +SUB"
                    e.Node.Name &= " :SUB"
                    ADDPathTOGroup(e.Node.Name)
                Else
                    ADDPathTOGroup(e.Node.Name)
                End If
            End If
        End If
        bNodeIsChecked = False
        bSubs = False
        Cursor = Cursors.Default
    End Sub

    ' Captures the node state before a click and distinguishes normal selection from :SUB selection.
    ' Right-click toggles the checkbox manually because the TreeView does not do this by default.
    Private Sub TreeView1_MouseDown(sender As Object, e As MouseEventArgs) Handles TreeView1.MouseDown
        Dim mousePoint As Point = TreeView1.PointToClient(Control.MousePosition)
        Dim geklickterNode As TreeNode = TreeView1.GetNodeAt(mousePoint.X, mousePoint.Y)
        If geklickterNode IsNot Nothing Then bNodeIsChecked = geklickterNode.Checked
        If e.Button = MouseButtons.Right Then
            bSubs = False
            geklickterNode.Checked = Not geklickterNode.Checked
        Else
            bSubs = True
        End If
    End Sub

    ' Prevents expanding a node via the plus icon from being interpreted as a path-selection click.
    Private Sub Expand() Handles TreeView1.BeforeExpand
        bSubs = False
    End Sub

    ' Prevents collapsing a node via the plus icon from being interpreted as a path-selection click.
    Private Sub collapse() Handles TreeView1.BeforeCollapse
        bSubs = True
    End Sub

#End Region

#Region "User actions"
    ' Closes the form without applying the current selection to Findit6Agent.
    Private Sub bCancel_Click(sender As Object, e As EventArgs) Handles bCancel.Click
        Me.Close()
    End Sub

    ' Saves all path groups and makes the currently selected group the active Agent search space.
    ' The selected paths are written to AgentsIni and Standard.fij before the form closes.
    Private Sub bOK_Click(sender As Object, e As EventArgs) Handles bOK.Click
        If bNewGroup Then
            ' When a new group is created we give a short warning for first run
            MsgBoxNeu("First searches in new Groups may take longer when a path group contains many PDF files, Or when OCR Is enabled," & vbCrLf &
                "Findit6 may need to create text and OCR cache data during the first search. This initial search can therefore take longer than subsequent searches.")
        End If
        ' Persist all edited groups first.
        SavePathGroupInI()
        iGroupSelectedIndex = cmbGruppen.SelectedIndex + 1
        SelectedPathgroup = aPathGroupList(iGroupSelectedIndex - 1)
        frmMain.txtWhere.Text = SelectedPathgroup.Name
        ' Replace previous Paths in Agents ini with new ones
        Dim OldPathCount As Integer = AgentsIni.ReadValue("LoadedPathGroup", "Pathcount", "0")
        For i = 1 To OldPathCount
            AgentsIni.WriteValue("LoadedPathGroup", "Path" & i.ToString, Nothing)
        Next
        AgentsIni.WriteValue("LoadedPathGroup", "Name", SelectedPathgroup.Name)
        AgentsIni.WriteValue("LoadedPathGroup", "Number", iGroupSelectedIndex.ToString)
        AgentsIni.WriteValue("LoadedPathGroup", "Pathcount", SelectedPathgroup.Paths.Count.ToString)
        For j As Integer = 0 To SelectedPathgroup.Paths.Count - 1
            AgentsIni.WriteValue("LoadedPathGroup", "Path" & (j + 1).ToString(), SelectedPathgroup.Paths(j))
        Next
        ' Replace the previous search paths in Standard.fij with the newly selected group.
        OldPathCount = StandardFij.ReadValue("Where", "Pathcount", "0")
        For i = 1 To OldPathCount
            StandardFij.WriteValue("Where", "Path" & i.ToString, Nothing)
        Next
        StandardFij.WriteValue("Where", "Name", SelectedPathgroup.Name)
        StandardFij.WriteValue("Where", "Pathcount", SelectedPathgroup.Paths.Count.ToString)
        For j As Integer = 0 To SelectedPathgroup.Paths.Count - 1
            StandardFij.WriteValue("Where", "Path" & (j + 1).ToString(), SelectedPathgroup.Paths(j))
        Next
        Me.Close()
    End Sub

    ' Switches the editor to another path group selected in the ComboBox.
    ' The previous group is validated/compacted first, then the new group is rendered in the TreeView.
    Private Sub cmbGruppen_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbGruppen.SelectedIndexChanged
        If bGroupload Then Exit Sub
        If CheckEmptyGroup() = True Then
            cmbGruppen.SelectedIndex = iGroupSelectedIndex
            Exit Sub
        End If
        RemoveRedundantPaths()
        For Each rootNode As TreeNode In TreeView1.Nodes
            ClearAllTags(rootNode)
        Next
        iGroupSelectedIndex = cmbGruppen.SelectedIndex
        arrAktiveGroup = aPathGroupList(iGroupSelectedIndex)
        TxtbxBeschreibung.Text = arrAktiveGroup.Description
        TxtbxBeschreibung.Refresh()
        GruppenpfadeInTree()
    End Sub

    ' Validates that the active group contains at least one path when completeness checking is enabled.
    ' Returns True when the group is incomplete and the current user action should be cancelled.
    Private Function CheckEmptyGroup() As Boolean
        If bCheckGroupComplete And arrAktiveGroup.Paths.Count = 0 Then
            MsgBoxNeu("This Group does not contain any Path" & vbCrLf & "Select at least one or delete this group", MsgBoxStyle.OkOnly)
            Return True
        End If
        Return False
    End Function

    ' Creates a new empty path group after obtaining its name from frmNaming.
    ' The new group is added to both the in-memory list and the group selector.
    Private Sub bNeu_Click(sender As Object, e As EventArgs) Handles bNeu.Click
        bCheckGroupComplete = False
        frmNaming.bNew = True
        frmNaming.ShowDialog(Me)
        If sNewGRPname = "" Then Exit Sub
        Dim pg As New PathGroup
        bNewGroup = True
        aPathGroupList.Add(pg)
        cmbGruppen.Items.Add("")
        Dim newIndex As Integer = cmbGruppen.Items.Count - 1
        cmbGruppen.SelectedIndex = newIndex
        cmbGruppen.Items(newIndex) = sNewGRPname
        aPathGroupList(newIndex).Name = sNewGRPname
        sNewGRPname = ""
        bCheckGroupComplete = True
    End Sub

    ' Deletes the currently selected path group from memory and from the group selector.
    ' Selects a neighbouring group afterwards or clears the TreeView when no groups remain.
    Private Sub bDel_Click(sender As Object, e As EventArgs) Handles bDel.Click
        bCheckGroupComplete = False
        Dim index As Integer = cmbGruppen.SelectedIndex
        If index < 0 Then Exit Sub
        aPathGroupList.RemoveAt(index)
        cmbGruppen.Items.RemoveAt(index)
        If cmbGruppen.Items.Count > 0 Then
            If index >= cmbGruppen.Items.Count Then
                cmbGruppen.SelectedIndex = cmbGruppen.Items.Count - 1
            Else
                cmbGruppen.SelectedIndex = index
            End If
        Else
            cmbGruppen.Text = ""
            DeselectNodes(TreeView1.Nodes)
            TreeView1.CollapseAll()
        End If
        bCheckGroupComplete = True
    End Sub

    ' Clears the highlighted TreeView node when focus leaves the directory tree.
    Private Sub TreeView1_LostFocus(sender As Object, e As EventArgs) Handles TreeView1.LostFocus
        TreeView1.SelectedNode = Nothing
    End Sub

    ' Stores the edited group description and restores the placeholder label when the field is empty.
    Private Sub TxtbxBeschreibung_Leave(sender As Object, e As EventArgs) Handles TxtbxBeschreibung.Leave
        arrAktiveGroup.Description = TxtbxBeschreibung.Text
        If TxtbxBeschreibung.Text = "" Then
            lblBeschreibung.Visible = True
        Else
            lblBeschreibung.Visible = False
        End If
    End Sub

    ' Shows the description placeholder only while the description field is empty.
    Private Sub TxtbxBeschreibung_TextChanged(sender As Object, e As EventArgs) Handles TxtbxBeschreibung.TextChanged
        If TxtbxBeschreibung.Text = "" Then
            lblBeschreibung.Visible = True
        Else
            lblBeschreibung.Visible = False
        End If
    End Sub

    ' Hides the description placeholder while the user edits the field.
    Private Sub TxtbxBeschreibung_GotFocus(sender As Object, e As EventArgs) Handles TxtbxBeschreibung.GotFocus
        lblBeschreibung.Visible = False
    End Sub

    ' Opens frmNaming in rename mode for the currently selected path group.
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles bRename.Click
        frmNaming.bNew = False
        frmNaming.ShowDialog(Me)
    End Sub

    ' Prevents switching groups while the current group is invalid/empty.
    Private Sub cmbGruppen_MouseDown(sender As Object, e As MouseEventArgs) Handles cmbGruppen.MouseDown
        If CheckEmptyGroup() = True Then Exit Sub
    End Sub

#End Region
End Class
