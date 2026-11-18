using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace CrosshairStudio
{
    internal partial class StudioWindow
    {
        private string query="", category="";
        private bool favoriteOnly, libraryHistory;
        private Preset librarySelection;
        private int editingProfile=-1;
        private TextBlock homeState, homeGame;
        private void Home(StackPanel body)
        {
            var current=LivePreset();
            var profile=ActiveProfile();
            PageHeading(body,T("Visão geral","Overview"),T("Visualize sua mira, controle o overlay e acesse seus favoritos.","Preview your crosshair, control the overlay, and access your favorites."));
            var hero=Columns(-1,16,248);
            var panel=Col();
            var heading=new DockPanel { Margin=VerticalSpace(0,12) };
            var cat=Mono(current.Category.ToUpperInvariant(),10,"Cyan"); cat.VerticalAlignment=VerticalAlignment.Center;
            DockPanel.SetDock(cat,Dock.Right); heading.Children.Add(cat);
            heading.Children.Add(Label(T("Mira ativa","Active crosshair"),15,"Text",true)); panel.Children.Add(heading);
            var preview=PreviewOf(current,306,4,true,true);
            var canvas=new Grid(); canvas.Children.Add(preview);
            var legend=Mono(T("PRÉVIA   /   4×","PREVIEW   /   4×"),10,"Cyan");
            legend.Margin=new Thickness(26,23,0,0); legend.HorizontalAlignment=HorizontalAlignment.Left; legend.VerticalAlignment=VerticalAlignment.Top;
            legend.IsHitTestVisible=false; canvas.Children.Add(legend); panel.Children.Add(canvas);
            var details=new DockPanel { Margin=VerticalSpace(12) };
            var launch=IconButton("next",T("Abrir editor","Open editor"),delegate
            {
                if(draft==null || draft.Id!=current.Id) { if(!DiscardDraft()) return; LoadDraft(ActivePreset()); RefreshRuntime(true); }
                Navigate("editor");
            });
            DockPanel.SetDock(launch,Dock.Right); details.Children.Add(launch);
            var captions=Col(); captions.Children.Add(Label(current.Name,21,"Text",true));
            captions.Children.Add(Label(current.Layers.Count + " " + T("camada(s)","layer(s)"),12,"Muted"));
            details.Children.Add(captions); panel.Children.Add(details);
            Add(hero,Panel(panel,14));

            var controls=Col();
            var runtime=Section("Overlay",T("Sobreposição na área de trabalho","Desktop overlay"));
            var enabled=Button(data.Overlay?T("Desativar mira","Turn overlay off"):T("Ativar mira","Turn overlay on"),Toggle,!data.Overlay);
            enabled.Margin=VerticalSpace(4,8); enabled.HorizontalAlignment=HorizontalAlignment.Stretch; runtime.Children.Add(enabled);
            homeState=Label("",11,"Muted");
            runtime.Children.Add(homeState);
            controls.Children.Add(Panel(runtime,14));
            var active=Section(T("Perfil em uso","Current profile"),"");
            var choices=new[]{T("Automático","Automatic")}.Concat(data.Profiles.Select(item=>item.Name));
            var switchProfile=Select(T("Trocar perfil","Switch profile"),choices,data.AutoProfile?0:effective+1,index=>
            {
                if(index>0) { SelectProfile(index-1,true); return; }
                data.AutoProfile=true;
                Changed(); Navigate("home",false,true);
                Notice(T("Seleção automática de perfil ativada.","Automatic profile selection enabled."));
            });
            switchProfile.ToolTip=T("Automático usa o perfil associado ao app em primeiro plano. Sem correspondência, vale o último perfil selecionado manualmente.",
                "Automatic uses the profile associated with the foreground app. Without a match, the last manually selected profile is used.");
            switchProfile.Margin=VerticalSpace(9,7); active.Children.Add(switchProfile);
            if(data.AutoProfile) active.Children.Add(Label(profile.Name,13,"Text",true));
            var screen=Desktop.Monitor(profile.Monitor);
            active.Children.Add(Mono(screen.Bounds.Width+" × "+screen.Bounds.Height+"   /   "+Format(profile.Scale)+"%",11));
            homeGame=Label("",11,"Muted");
            homeGame.Margin=VerticalSpace(7,7); active.Children.Add(homeGame);
            var profiles=IconButton("profile",T("Gerenciar perfis","Manage profiles"),delegate { Navigate("profiles"); });
            profiles.Margin=VerticalSpace(4); active.Children.Add(profiles);
            var activeCard=Panel(active,14); activeCard.Margin=VerticalSpace(12); controls.Children.Add(activeCard);
            var keys=Section(T("Atalhos rápidos","Quick shortcuts"),"");
            keys.Children.Add(KeyLine(T("Mostrar / ocultar","Show / hide"),data.Hotkeys["toggle"]));
            keys.Children.Add(KeyLine(T("Próxima favorita","Next favorite"),data.Hotkeys["next"]));
            keys.Children.Add(KeyLine(T("Abrir editor","Open editor"),data.Hotkeys["editor"]));
            var shortcutCard=Panel(keys,14); shortcutCard.Margin=VerticalSpace(12); controls.Children.Add(shortcutCard);
            Add(hero,controls,2); body.Children.Add(hero);
            var favoritesHead=new DockPanel { Margin=VerticalSpace(18,8) };
            var all=IconButton("next",T("Ver biblioteca","Browse library"),delegate { Navigate("library"); });
            DockPanel.SetDock(all,Dock.Right); favoritesHead.Children.Add(all);
            var title=Label(T("Favoritas","Favorites"),17,"Text",true); title.VerticalAlignment=VerticalAlignment.Center; favoritesHead.Children.Add(title);
            body.Children.Add(favoritesHead);
            var favorites=Columns(-1,-1,-1,-1);
            var list=data.Presets.Where(p=>p.Favorite).Take(4).ToArray();
            for(int i=0;i<list.Length;i++)
            {
                var p=list[i];
                var card=PresetButton(p,delegate { ActivatePreset(p); Navigate("home",false,true); },66);
                card.Margin=new Thickness(0,0,8,0); Add(favorites,card,i);
            }
            if(list.Length==0) body.Children.Add(Label(T("Marque miras na biblioteca para acessá-las aqui.","Star crosshairs in your library to see them here."),13,"Muted"));
            else body.Children.Add(favorites);
        }
        private FrameworkElement KeyLine(string action,string key)
        {
            var row = new DockPanel { Margin=VerticalSpace(8) };
            var chord=Mono(key.Length>0?key:"—",10,"Accent"); DockPanel.SetDock(chord,Dock.Right); row.Children.Add(chord);
            var name=Label(action,11,"Muted"); row.Children.Add(name); return row;
        }
        private Button PresetButton(Preset p,Action click,double height)
        {
            var b=Button(p.Name,click); b.HorizontalContentAlignment=HorizontalAlignment.Stretch; b.Padding=new Thickness(6);
            if(p.Id==ActivePreset().Id) { Ref(b,Control.BorderBrushProperty,"Accent"); Ref(b,Control.BackgroundProperty,"Active"); }
            var card=Col(); card.Children.Add(PreviewOf(p,height,1.7,false));
            var label=Label(p.Name,12,"Text",true); label.Margin=VerticalSpace(5); label.TextWrapping=TextWrapping.NoWrap; label.TextTrimming=TextTrimming.CharacterEllipsis;
            card.Children.Add(label); var categoryLabel=Mono(p.Category,10); categoryLabel.Margin=VerticalSpace(3,1); card.Children.Add(categoryLabel);
            b.Content=card; return b;
        }

        private void Library(StackPanel body)
        {
            PageHeading(body,T("Biblioteca de miras","Crosshair library"),T("Explore presets, organize favoritos e compartilhe suas criações.","Browse presets, organize favorites, and share your creations."));
            var bar=new WrapPanel { Margin=VerticalSpace(0,12) };
            var search=TextField(T("Buscar mira","Search crosshairs"),query,null,70); search.Width=176; search.Margin=new Thickness(0,0,8,5);
            bar.Children.Add(search);
            var labels=new[] {T("Todas as categorias","All categories")}.Concat(Catalog.Categories).ToArray();
            string[] keys=new[]{""}.Concat(Catalog.Categories).ToArray();
            var filter=Select(T("Categoria","Category"),labels,Array.IndexOf(keys,category),i=>{ category=keys[i]; if(fillLibrary!=null) fillLibrary(); });
            filter.Width=152; filter.Margin=new Thickness(0,0,8,5); bar.Children.Add(filter);
            var favorite=Check(T("Favoritas","Favorites"),favoriteOnly,b=>{ favoriteOnly=b; if(fillLibrary!=null) fillLibrary(); }); favorite.Margin=new Thickness(0,7,12,0); bar.Children.Add(favorite);
            bar.Children.Add(Button(T("Importar JSON","Import JSON"),ImportPreset));
            bar.Children.Add(Button(T("Colar código","Paste code"),ImportCode));
            bar.Children.Add(Button(libraryHistory?T("Ver coleção","Show collection"):T("Histórico","History"),delegate { libraryHistory=!libraryHistory; Navigate("library",false,true); }));
            body.Children.Add(bar);
            if(libraryHistory)
            {
                search.IsEnabled=filter.IsEnabled=favorite.IsEnabled=false;
                if(data.History.Count==0) body.Children.Add(Panel(Label(T("As revisões aparecem aqui quando uma mira salva é editada.","Revisions appear here after editing a saved crosshair."),13,"Muted")));
                foreach(var revision in data.History)
                {
                    var row=new DockPanel();
                    var restore=Button(T("Restaurar como cópia","Restore as copy"),delegate { DuplicatePreset(revision.Preset,T(" revisão"," revision")); Navigate("library",false,true); });
                    DockPanel.SetDock(restore,Dock.Right); row.Children.Add(restore);
                    var content=Col(); content.Children.Add(Label(revision.Preset.Name,14,"Text",true)); content.Children.Add(Mono(revision.When)); row.Children.Add(content);
                    var card=Panel(row,11); card.Margin=VerticalSpace(0,6); body.Children.Add(card);
                }
                return;
            }
            var workspace=Columns(-1,14,225);
            var cards=Col(); Add(workspace,cards);
            var selection=Col(); Add(workspace,Panel(selection,12),2); body.Children.Add(workspace);
            Action details=delegate
            {
                selection.Children.Clear();
                var p=librarySelection;
                if(p==null) { selection.Children.Add(Label(T("Selecione uma mira para pré-visualizar.","Select a crosshair to preview."),13,"Muted")); return; }
                selection.Children.Add(Label(p.Name,17,"Text",true));
                var preview=PreviewOf(p,153,3);
                preview.Margin=VerticalSpace(9,8); selection.Children.Add(preview);
                selection.Children.Add(Label(p.Category+"  /  "+p.Layers.Count+" "+T("camada(s)","layer(s)"),12,"Muted"));
                var group=Field(T("Categoria","Category"),Select(T("Categoria da mira","Crosshair category"),Catalog.Categories,
                    Array.IndexOf(Catalog.Categories,p.Category),i=>{ p.Category=Catalog.Categories[i]; Changed(); if(fillLibrary!=null) fillLibrary(); }));
                selection.Children.Add(group);
                var actions=new WrapPanel { Margin=VerticalSpace(9) };
                actions.Children.Add(Button(T("Aplicar","Apply"),delegate { ActivatePreset(p); if(fillLibrary!=null) fillLibrary(); },true));
                actions.Children.Add(Button(T("Editar","Edit"),delegate { if(!DiscardDraft()) return; LoadDraft(p); RefreshRuntime(true); Navigate("editor"); }));
                actions.Children.Add(Button(T("Duplicar","Duplicate"),delegate { librarySelection=DuplicatePreset(p,T(" cópia"," copy")); Navigate("library",false,true); }));
                actions.Children.Add(Button(T("Exportar","Export"),delegate { ExportPreset(p); }));
                actions.Children.Add(Button(T("Copiar código","Copy code"),delegate { CopyCode(p); }));
                actions.Children.Add(Button(p.Favorite?T("Remover favorita","Remove favorite"):T("Favoritar","Favorite"),
                    delegate { p.Favorite=!p.Favorite; Changed(); if(fillLibrary!=null) fillLibrary(); }));
                actions.Children.Add(Button(T("Excluir","Delete"),delegate { DeletePreset(p); }));
                foreach(Button action in actions.Children) action.Margin=new Thickness(0,0,5,6);
                selection.Children.Add(actions);
                if(p.Favorite) selection.Children.Add(Mono(T("★   FAVORITA","★   FAVORITE"),10,"Accent"));
            };
            fillLibrary=delegate
            {
                cards.Children.Clear();
                var list=data.Presets.Where(p=>(category==""||p.Category==category)&&(!favoriteOnly||p.Favorite)&&
                    (query.Length==0||p.Name.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0)).ToArray();
                if(!list.Contains(librarySelection)) librarySelection=list.FirstOrDefault();
                if(list.Length==0) cards.Children.Add(Panel(Label(T("Nenhuma mira com esses filtros.","No crosshairs match these filters."),13,"Muted")));
                Grid row=null;
                for(int i=0;i<list.Length;i++)
                {
                    var p=list[i];
                    if(i%3==0) { row=Columns(-1,-1,-1); row.Margin=VerticalSpace(0,8); cards.Children.Add(row); }
                    var item=PresetButton(p,delegate { librarySelection=p; details(); },71);
                    Add(row,item,i%3);
                }
                details();
            };
            search.TextChanged+=delegate { query=search.Text.Trim(); fillLibrary(); };
            fillLibrary();
        }
        private Action fillLibrary;
        private Preset DuplicatePreset(Preset p,string suffix)
        {
            var copy=Store.Clone(p); copy.Id=Guid.NewGuid().ToString("N"); copy.Name=Store.Limit(copy.Name,48,"")+suffix; copy.Favorite=false;
            AddPreset(copy); Changed(); Notice(T("Cópia salva na biblioteca.","Copy saved to the library.")); return copy;
        }
        private void DeletePreset(Preset p)
        {
            if(data.Presets.Count==1) throw new InvalidDataException(T("Mantenha pelo menos uma mira.","Keep at least one crosshair."));
            if(draft!=null && draft.Id==p.Id && !DiscardDraft()) return;
            if(MessageBox.Show(this,T("Excluir a mira da biblioteca?","Delete this crosshair from your library?"),"Crosshair Studio",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
            data.Presets.Remove(p);
            foreach(var profile in data.Profiles.Where(x=>x.PresetId==p.Id)) profile.PresetId=data.Presets[0].Id;
            if(draft!=null && draft.Id==p.Id) { draft=null; dirty=false; }
            Changed(); Navigate("library",false,true);
        }
        private void CopyCode(Preset preset)
        {
            var code=Store.Share(preset);
            try { Clipboard.SetText(code); }
            catch(ExternalException)
            {
                Notice(T("A área de transferência está ocupada. Tente copiar novamente.","The clipboard is busy. Try copying again."));
                return;
            }
            Notice(T("Código da mira copiado.","Crosshair code copied."));
        }
        private void ImportCode()
        {
            string title=T("Colar código da mira","Paste crosshair code");
            var dialog=new Window { Owner=this, ShowInTaskbar=false, Title=title, Width=465, SizeToContent=SizeToContent.Height,
                WindowStartupLocation=WindowStartupLocation.CenterOwner, ResizeMode=ResizeMode.NoResize, FontFamily=FontFamily };
            foreach(var dictionary in Resources.MergedDictionaries) dialog.Resources.MergedDictionaries.Add(dictionary);
            foreach(var key in Resources.Keys) dialog.Resources[key]=Resources[key];
            dialog.SetResourceReference(BackgroundProperty,"Panel");
            var content=Col(); content.Margin=new Thickness(17);
            content.Children.Add(Label(T("Cole um código do Crosshair Studio que comece com CSX- ou CS1-.","Paste a Crosshair Studio code that starts with CSX- or CS1-."),13,"Muted"));
            var box=TextField(title,"",null,ShareCode.MaxCodeLength,true); box.Margin=VerticalSpace(10,10);
            box.Height=105; box.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
            box.FontFamily=new FontFamily("Consolas");
            try
            {
                string text=Clipboard.GetText().Trim();
                if(ShareCode.HasPrefix(text) && text.Length<=ShareCode.MaxCodeLength) box.Text=text;
            }
            catch(ExternalException) { }
            content.Children.Add(box);
            var error=Label("",12,"Muted");
            error.Margin=VerticalSpace(0,10); error.Visibility=Visibility.Collapsed; content.Children.Add(error);
            Preset imported=null;
            var row=Row();
            var ok=Button(T("Importar mira","Import crosshair"),delegate
            {
                try { imported=Store.Unshare(box.Text); }
                catch(InvalidDataException ex)
                {
                    error.Text=ex.Message; error.Visibility=Visibility.Visible; return;
                }
                dialog.DialogResult=true;
            },true);
            ok.IsDefault=true; row.Children.Add(ok);
            var cancel=Button(T("Cancelar","Cancel"),delegate { dialog.DialogResult=false; }); cancel.IsCancel=true; row.Children.Add(cancel);
            content.Children.Add(row); dialog.Content=content; dialog.Loaded+=delegate { box.Focus(); };
            if(dialog.ShowDialog()!=true || (page=="editor" && !DiscardDraft())) return;
            AcceptImport(imported,true);
            Notice(T("Mira importada e aplicada  /  ","Crosshair imported and applied  /  ")+imported.Name);
        }

        private void Profiles(StackPanel body)
        {
            PageHeading(body,T("Perfis de jogo","Game profiles"),T("Use o mesmo perfil de mira e monitor em vários jogos ou aplicativos.","Use the same crosshair and monitor profile across multiple games or apps."));
            if(editingProfile<0 || editingProfile>=data.Profiles.Count) editingProfile=effective;
            var profile=data.Profiles[editingProfile];
            var tools=new WrapPanel { Margin=VerticalSpace(0,13) };
            var templates=Select(T("Modelo de perfil","Profile template"),new[]{T("+ Adicionar perfil","+ Add profile"),T("Padrão","Standard"),"FPS",T("Minimalista","Minimal"),T("Competitivo","Competitive")},0,i=>{ if(i>0) NewProfile(i); });
            templates.Width=175; templates.Margin=new Thickness(0,0,9,0); tools.Children.Add(templates);
            tools.Children.Add(Check(T("Trocar de perfil automaticamente","Switch profiles automatically"),data.AutoProfile,value=>{ data.AutoProfile=value; Changed(); Navigate("profiles",false,true); }));
            body.Children.Add(tools);
            var grid=Columns(196,13,-1);
            var list=Col(); list.Children.Add(Label(T("Seus perfis","Your profiles"),16,"Text",true));
            for(int i=0;i<data.Profiles.Count;i++)
            {
                int index=i; var p=data.Profiles[i];
                var b=Button((effective==i?"●  ":"")+p.Name,delegate { editingProfile=index; Navigate("profiles",false,true); });
                b.Style=(Style)FindResource("NavButton"); b.Tag=i==editingProfile?"Selected":null; b.Margin=VerticalSpace(4); list.Children.Add(b);
            }
            Add(grid,Panel(new ScrollViewer { Content=list,MaxHeight=440,VerticalScrollBarVisibility=ScrollBarVisibility.Auto },11));
            var config=Col();
            var title=Label(profile.Name,19,"Text",true); config.Children.Add(title);
            var columns=Columns(-1,12,-1);
            var name=TextField(T("Nome do perfil","Profile name"),profile.Name,value=>{ profile.Name=Store.Limit(value,60,T("Padrão","Default")); Changed(); Navigate("profiles",false,true); },60);
            Add(columns,Field(T("Nome","Name"),name));
            var selected=Select(T("Mira do perfil","Profile crosshair"),data.Presets.Select(p=>p.Name),data.Presets.FindIndex(p=>p.Id==profile.PresetId),
                i=>{ profile.PresetId=data.Presets[i].Id; Changed(); });
            Add(columns,Field(T("Mira","Crosshair"),selected),2); config.Children.Add(columns);
            config.Children.Add(ProfileExecutables(profile));
            var monitors=Forms.Screen.AllScreens;
            var monitorIds=new[]{""}.Concat(monitors.Select(s=>s.DeviceName)).ToArray();
            var monitorNames=new[]{T("Monitor principal (automático)","Primary display (automatic)")}.Concat(monitors.Select((s,i)=>
                (i+1)+"  ·  "+s.Bounds.Width+" × "+s.Bounds.Height+"   "+(s.Primary?T("Principal","Primary"):""))).ToArray();
            int selectedMonitor=Array.IndexOf(monitorIds,profile.Monitor??"");
            config.Children.Add(Field(T("Monitor","Display"),Select(T("Monitor","Display"),monitorNames,Math.Max(0,selectedMonitor),i=>{ profile.Monitor=monitorIds[i]; Changed(); })));
            var positioning=Columns(-1,8,-1,8,-1);
            Add(positioning,Field("X (px)",TextField("X",Format(profile.X),v=> { profile.X=(int)Store.Clamp(Parse(v),-4096,4096,0); Changed(); },9)));
            Add(positioning,Field("Y (px)",TextField("Y",Format(profile.Y),v=> { profile.Y=(int)Store.Clamp(Parse(v),-4096,4096,0); Changed(); },9)),2);
            Add(positioning,Field(T("Escala (%)","Scale (%)"),TextField(T("Escala","Scale"),Format(profile.Scale),v=> { profile.Scale=Store.Clamp(Parse(v),25,200,100); Changed(); },8)),4);
            config.Children.Add(positioning);
            var flags=new WrapPanel { Margin=VerticalSpace(6,8) };
            foreach(var flag in new[] { Check(T("Mira permitida","Overlay allowed"),profile.Enabled,v=>{ profile.Enabled=v; Changed(); }),
                Check(T("Mira só nos apps da lista","Overlay only in listed apps"),profile.OnlyForeground,v=>{ profile.OnlyForeground=v; Changed(); }),
                Check(T("Animar mira","Animate crosshair"),profile.Animated,v=>{ profile.Animated=v; Changed(); }) })
            { flag.Margin=new Thickness(0,5,13,5); flags.Children.Add(flag); }
            config.Children.Add(flags);
            var row=new WrapPanel();
            row.Children.Add(Button(T("Ativar manualmente","Activate manually"),()=>SelectProfile(editingProfile,true),true));
            row.Children.Add(Button(T("Centralizar","Center"),delegate { profile.X=profile.Y=0; Changed(); Navigate("profiles",false,true); }));
            row.Children.Add(Button(T("Excluir","Delete"),DeleteProfile));
            config.Children.Add(row); Add(grid,Panel(config),2); body.Children.Add(grid);
            var exceptions=Section(T("Exceções","Exceptions"),T("A mira fica oculta quando um destes executáveis estiver em primeiro plano. Use um caminho ou nome .exe por linha.","The overlay is hidden when any of these executables is in the foreground. Use one .exe path or filename per line."));
            var exclusionBox=TextField(T("Lista de exceções","Exception list"),string.Join(Environment.NewLine,data.Exclusions),null,8192,true);
            exclusionBox.Height=70; exclusionBox.VerticalScrollBarVisibility=ScrollBarVisibility.Auto; exceptions.Children.Add(exclusionBox);
            var commit=Button(T("Salvar exceções","Save exceptions"),delegate
            {
                var items=exclusionBox.Text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(v=>v.Trim()).Where(v=>v.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if(items.Count>80 || items.Any(v=>v.Length>1024 || !v.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException(T("Use até 80 entradas terminadas em .exe.","Use up to 80 entries ending in .exe."));
                data.Exclusions=items; Changed(); Notice(T("Exceções salvas.","Exceptions saved."));
            }); commit.HorizontalAlignment=HorizontalAlignment.Left; commit.Margin=VerticalSpace(8); exceptions.Children.Add(commit);
            var box=Panel(exceptions); box.Margin=VerticalSpace(12); body.Children.Add(box);
        }
        private FrameworkElement ProfileExecutables(Profile profile)
        {
            var section=Section(T("Jogos e aplicativos associados","Associated games and apps"),
                T("Digite um nome como jogo.exe ou selecione um ou mais arquivos.",
                    "Enter a name such as game.exe or select one or more files."));
            section.Margin=VerticalSpace(12,10);
            var addRow=new DockPanel();
            var input=TextField(T("Nome ou caminho do executável","Executable name or path"),"",null,Profile.ExecutableMaxLength);
            var entries=Col();
            Action renderEntries=null;
            renderEntries=delegate
            {
                entries.Children.Clear();
                if(profile.Executables.Count==0)
                    entries.Children.Add(Label(T("Nenhum executável associado.","No associated executables."),12,"Muted"));
                foreach(var executable in profile.Executables)
                {
                    var row=new DockPanel();
                    row.Margin=VerticalSpace(0,5);
                    var remove=Button(T("Remover","Remove"),delegate
                    {
                        profile.Executables.Remove(executable);
                        Changed();
                        renderEntries();
                    });
                    remove.Margin=new Thickness(0);
                    remove.MinHeight=30;
                    AutomationProperties.SetName(remove,T("Remover ","Remove ")+executable);
                    DockPanel.SetDock(remove,Dock.Right);
                    row.Children.Add(remove);
                    var name=Label(executable,12);
                    name.TextWrapping=TextWrapping.NoWrap;
                    name.TextTrimming=TextTrimming.CharacterEllipsis;
                    name.ToolTip=executable;
                    name.Margin=new Thickness(0,0,10,0);
                    name.VerticalAlignment=VerticalAlignment.Center;
                    row.Children.Add(name);
                    entries.Children.Add(row);
                }
            };
            Action addTyped=delegate
            {
                if(!AddProfileExecutables(profile,new[]{input.Text})) return;
                input.Clear();
                renderEntries();
                input.Focus();
            };
            var add=Button(T("Adicionar","Add"),addTyped);
            add.Margin=new Thickness(8,0,0,0);
            DockPanel.SetDock(add,Dock.Right);
            addRow.Children.Add(add);
            addRow.Children.Add(input);
            input.KeyDown+=delegate(object sender,KeyEventArgs e)
            {
                if(e.Key!=Key.Enter) return;
                e.Handled=true;
                Safe(addTyped);
            };
            section.Children.Add(addRow);
            var browse=Button(T("Procurar executáveis…","Browse executables…"),delegate
            {
                var dialog=new OpenFileDialog { Filter=T("Executáveis|*.exe","Executables|*.exe"), Multiselect=true };
                if(dialog.ShowDialog(this)==true && AddProfileExecutables(profile,dialog.FileNames)) renderEntries();
            });
            browse.Margin=VerticalSpace(7,9);
            browse.HorizontalAlignment=HorizontalAlignment.Left;
            section.Children.Add(browse);
            section.Children.Add(new ScrollViewer { Content=entries, MaxHeight=160,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            renderEntries();
            return section;
        }
        private bool AddProfileExecutables(Profile profile,IEnumerable<string> values)
        {
            var executables=values.Select(Profile.NormalizeExecutable).ToArray();
            if(executables.Any(value=>value.Length>Profile.ExecutableMaxLength || !value.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(T("Informe um nome ou caminho terminado em .exe.","Enter a filename or path ending in .exe."));
            var additions=executables.Except(profile.Executables,StringComparer.OrdinalIgnoreCase).ToArray();
            if(additions.Length==0)
            {
                Notice(T("Os executáveis selecionados já estão na lista.","The selected executables are already listed."));
                return false;
            }
            profile.Executables.AddRange(additions);
            Changed();
            Notice(T("Lista de executáveis atualizada.","Executable list updated."));
            return true;
        }
        private void NewProfile(int template)
        {
            if(data.Profiles.Count>=80) throw new InvalidDataException(T("Limite de 80 perfis.","Maximum 80 profiles."));
            string[] names={T("Novo perfil","New profile"),"FPS",T("Minimalista","Minimal"),T("Competitivo","Competitive")};
            string[] defaults={ActivePreset().Id,"vector","signal","sentry"};
            var p=new Profile { Name=names[template-1], PresetId=data.Presets.Any(preset=>preset.Id==defaults[template-1])?defaults[template-1]:ActivePreset().Id,
                Scale=template==3?80:100, Animated=template!=4 };
            data.Profiles.Add(p); editingProfile=data.Profiles.Count-1; Changed(); Navigate("profiles",false,true);
        }
        private void DeleteProfile()
        {
            if(data.Profiles.Count==1) throw new InvalidDataException(T("Mantenha pelo menos um perfil.","Keep at least one profile."));
            if(MessageBox.Show(this,T("Excluir o perfil selecionado?","Delete the selected profile?"),"Crosshair Studio",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
            int removed=editingProfile;
            data.Profiles.RemoveAt(removed);
            if(data.ManualProfile>removed) data.ManualProfile--;
            else if(data.ManualProfile==removed) data.ManualProfile=Math.Min(removed,data.Profiles.Count-1);
            effective=data.ManualProfile; editingProfile=effective; Changed(); Navigate("profiles",false,true);
        }

        private int compareLeft, compareRight=1, compareZoom=3;
        private string contrastBackground="Lab";
        private void Compare(StackPanel body)
        {
            PageHeading(body,T("Comparar miras","Compare crosshairs"),T("Coloque duas miras lado a lado no mesmo fundo e com o mesmo zoom.","Place two crosshairs side by side on the same background at the same zoom."));
            var options=new WrapPanel { Margin=VerticalSpace(0,12) };
            var bg=Select(T("Fundo compartilhado","Shared background"),new[]{"Lab",T("Escuro","Dark"),T("Claro","Light")},Array.IndexOf(new[]{"Lab","Dark","Light"},contrastBackground),i=>
                { contrastBackground=new[]{"Lab","Dark","Light"}[i]; Navigate("compare",false,true); });
            bg.Width=112; bg.Margin=new Thickness(0,0,7,0); options.Children.Add(bg);
            var scale=Select("Zoom",new[]{"1×","2×","3×","4×","5×","6×"},compareZoom-1,i=>{ compareZoom=i+1; Navigate("compare",false,true); });
            scale.Width=69; options.Children.Add(scale); body.Children.Add(options);
            var grid=Columns(-1,14,-1);
            Add(grid,CompareCard(true)); Add(grid,CompareCard(false),2); body.Children.Add(grid);
        }
        private Border CompareCard(bool first)
        {
            int index=Math.Min(first?compareLeft:compareRight,data.Presets.Count-1);
            var current=data.Presets[index]; var stack=Col();
            var combo=Select(first?T("Mira esquerda","Left crosshair"):T("Mira direita","Right crosshair"),data.Presets.Select(p=>p.Name),index,i=>
                { if(first) compareLeft=i; else compareRight=i; Navigate("compare",false,true); });
            stack.Children.Add(combo);
            var preview=PreviewOf(current,310,compareZoom,true,true); preview.BackgroundMode=contrastBackground; preview.Refresh(); preview.Margin=VerticalSpace(9,9); stack.Children.Add(preview);
            stack.Children.Add(Button(T("Aplicar ao perfil","Apply to profile"),()=>ActivatePreset(current),true));
            return Panel(stack,12);
        }

        private void Preferences(StackPanel body)
        {
            PageHeading(body,T("Configurações","Settings"),T("Ajuste a interface, os atalhos e o comportamento do aplicativo.","Adjust the interface, shortcuts, and application behavior."));
            var grid=Columns(-1,13,-1);
            var general=Section(T("Interface e execução","Interface and startup"),"");
            general.Children.Add(Check(T("Tema claro","Light theme"),data.Light,b=>{ data.Light=b; ApplyTheme(); Changed(); }));
            general.Children.Add(Field(T("Idioma","Language"),Select(T("Idioma","Language"),new[]{"Português (Brasil)","English"},data.English?1:0,
                i=>{ data.English=i==1; Changed(); BuildChrome(); if(tray!=null) { DisposeTray(); CreateTray(); } Navigate("settings",false); })));
            general.Children.Add(Number(T("Escala da interface (%)","Interface scale (%)"),data.InterfaceScale,80,120,5,v=>{ data.InterfaceScale=v; ApplyTheme(); Changed(); }));
            general.Children.Add(Check(T("Minimizar e fechar para a bandeja","Minimize and close to tray"),data.ToTray,v=>{ data.ToTray=v; Changed(); }));
            general.Children.Add(Check(T("Iniciar com o Windows","Start with Windows"),File.Exists(Desktop.StartupFile()),v=>
            {
                try { Desktop.SetStartup(v); Notice(T("Inicialização atualizada.","Startup preference updated.")); }
                catch { Navigate("settings",false,true); throw; }
            }));
            general.Children.Add(Check(T("Permitir animação no overlay","Allow overlay motion"),data.Animate,v=>{ data.Animate=v; Changed(); }));
            general.Children.Add(Check(T("Modo desempenho (pausa animações)","Performance mode (pauses motion)"),data.Performance,v=>{ data.Performance=v; Changed(); }));
            var left=Col(); left.Children.Add(Panel(general,13));
            var backup=Section(T("Dados e diagnóstico","Data and diagnostics"),T("As configurações e o registro de erros ficam no computador.","Settings and error logs stay on this device."));
            var row=new WrapPanel();
            row.Children.Add(Button(T("Exportar backup","Export backup"),ExportBackup));
            row.Children.Add(Button(T("Restaurar backup","Restore backup"),ImportBackup));
            row.Children.Add(Button(T("Redefinir","Reset"),ResetSettings));
            foreach(Button b in row.Children) b.Margin=new Thickness(0,0,5,5);
            backup.Children.Add(row);
            var metrics=Mono(Diagnostic(),10); metrics.Margin=VerticalSpace(7,4); backup.Children.Add(metrics);
            var refresh=Button(T("Atualizar diagnóstico","Refresh diagnostics"),delegate { metrics.Text=Diagnostic(); });
            refresh.HorizontalAlignment=HorizontalAlignment.Left; backup.Children.Add(refresh);
            var backupBox=Panel(backup,13); backupBox.Margin=VerticalSpace(12); left.Children.Add(backupBox); Add(grid,left);
            var keys=Section(T("Atalhos globais","Global shortcuts"),T("Use Ctrl ou Alt com uma letra, número ou tecla de função. Deixe vazio para desativar.","Use Ctrl or Alt with a letter, digit, or function key. Leave blank to disable."));
            var fields=new Dictionary<string,TextBox>();
            for(int i=0;i<Actions.Length;i++)
            {
                string id=Actions[i]; string name=ActionName(i);
                var field=TextField(name,data.Hotkeys[id],null,45); fields[id]=field;
                keys.Children.Add(Field(name,field));
                if(hotkeys!=null && hotkeys.Failures.ContainsKey(id)) keys.Children.Add(Label("⚠  "+hotkeys.Failures[id],11,"Accent"));
            }
            var save=Button(T("Salvar atalhos","Save shortcuts"),delegate
            {
                var next=fields.ToDictionary(x=>x.Key,x=>x.Value.Text.Trim());
                var chords=new HashSet<string>();
                foreach(var item in next.Where(x=>x.Value.Length>0))
                {
                    uint mods,key;
                    if(!Hotkeys.TryParse(item.Value,out mods,out key)) throw new InvalidDataException(T("Combinação inválida: ","Invalid shortcut: ")+item.Value);
                    if(!chords.Add(mods+"/"+key)) throw new InvalidDataException(T("Combinação repetida: ","Duplicate shortcut: ")+item.Value);
                }
                if(hotkeys!=null)
                {
                    hotkeys.RegisterAll(next);
                    if(hotkeys.Failures.Count>0)
                    {
                        var failed=hotkeys.Failures.Keys.Select(k=>next[k]).ToArray();
                        hotkeys.RegisterAll(data.Hotkeys);
                        throw new InvalidDataException(T("Atalho em uso ou reservado: ","Shortcut in use or reserved: ")+string.Join(", ",failed));
                    }
                }
                data.Hotkeys=next; Changed(); Notice(T("Atalhos salvos.","Shortcuts saved.")); Navigate("settings",false,true);
            },true); save.Margin=VerticalSpace(7); keys.Children.Add(save);
            Add(grid,Panel(keys,13),2); body.Children.Add(grid);
        }
        private string ActionName(int i)
        {
            return new[]{T("Mostrar / ocultar","Show / hide"),T("Próxima mira","Next crosshair"),T("Mira anterior","Previous crosshair"),
                T("Abrir editor","Open editor"),T("Centralizar mira","Center crosshair"),T("Alternar monitor","Switch monitor"),
                T("Alternar perfil","Switch profile"),T("Alternar animação","Toggle motion")}[i];
        }
        private string Diagnostic()
        {
            using(var process=Process.GetCurrentProcess())
                return "RAM  "+Format(process.WorkingSet64/1048576d)+" MiB   •   DRAWS  "+overlay.Frames+"\n"+
                    T("ÚLTIMO DESENHO  ","LAST DRAW  ")+Format(overlay.LastMilliseconds)+" ms   •   "+T("MONITORES  ","DISPLAYS  ")+Forms.Screen.AllScreens.Length+
                    (lastLog.Length>0?"\n"+lastLog:"");
        }
        private void ExportBackup()
        {
            var dialog=new SaveFileDialog { Filter="Crosshair Studio backup|*.json", FileName="Crosshair-Studio-backup" };
            if(dialog.ShowDialog(this)!=true) return;
            Store.Atomic(dialog.FileName,Store.Encode(data),false); Notice(T("Backup exportado.","Backup exported."));
        }
        private void ImportBackup()
        {
            var dialog=new OpenFileDialog { Filter="Crosshair Studio backup|*.json" };
            if(dialog.ShowDialog(this)!=true) return;
            var replacement=Store.Validate(Store.Decode<Settings>(Store.Read(dialog.FileName,Store.MaxBytes)));
            if(!DiscardDraft()) return;
            ReplaceSettings(replacement); Notice(T("Backup restaurado.","Backup restored."));
        }
        private void ResetSettings()
        {
            if(MessageBox.Show(this,T("Redefinir miras, perfis e atalhos para os padrões?","Reset crosshairs, profiles, and shortcuts to defaults?"),
                "Crosshair Studio",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes || !DiscardDraft()) return;
            if(File.Exists(Desktop.StartupFile())) Desktop.SetStartup(false);
            ReplaceSettings(Settings.Fresh()); Notice(T("Preferências redefinidas.","Preferences reset."));
        }
        private void ReplaceSettings(Settings next)
        {
            data=next; draft=null; dirty=false; effective=data.ManualProfile; editingProfile=-1; librarySelection=null;
            if(hotkeys!=null) hotkeys.RegisterAll(data.Hotkeys);
            ApplyTheme(); BuildChrome();
            if(tray!=null) { DisposeTray(); CreateTray(); }
            Changed(); Navigate("settings",false);
        }
    }
}
