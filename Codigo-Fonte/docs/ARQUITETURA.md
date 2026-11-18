# Arquitetura e pesquisa

## Referência XyCrosshair

A análise usou o ZIP local **XyCrosshair-main**, SHA-256 **213A64E44E363ACF5D944E4107EE3E3C388005CEC0162BF8E2BC1E0219B4EB30**. As duas cópias locais tinham o mesmo hash. A inspeção cobriu estrutura do repositório, compilação, dependências, pontos de entrada, módulos, empacotamento, licenças e mecanismos de overlay.

- **CMakeLists.txt** fixa o C++20 e baixa GLFW 3.4, ImGui 1.91.9b, nlohmann/json 3.11.3 e um commit de stb com hashes. Usa OpenGL. Há código de plataforma para Win32 e X11.
- **app.cpp / overlay.cpp** separam a janela do editor da sobreposição. O overlay GLFW tem framebuffer transparente, passagem do mouse e contexto OpenGL compartilhado. Um dos pontos reaproveitáveis como arquitetura conceitual é compartilhar a definição gráfica entre preview e overlay, com atualização sob demanda.
- **platform_win32.cpp / platform_x11.cpp** contém bandeja, registro de atalhos, janela de serviço e lógica por plataforma. A implementação Windows usa uma thread para mensagens, mantém o overlay no topo e usa os estilos de janela não ativável. A configuração usa diretório local ou portátil.
- **crosshair.cpp / library.cpp / settings.cpp** delegam geometria, bibliotecas de imagens e persistência para módulos dedicados. A biblioteca prepara imagens em segundo plano, usa texturas e cache. **ui.cpp / theme.cpp** apresentam o fluxo em ImGui; os scripts em **tools** lidam com conversão de biblioteca e material de desenvolvimento.
- **LICENSE** atribui MIT a Mrmariix em 2026. O aplicativo usa código novo e presets e ícones próprios. O único material redistribuído do ZIP são os arquivos Inter fornecidos com a licença SIL OFL 1.1. As imagens de comunidade, screenshots, fonte extra e branding do repositório ficaram fora do pacote. [Repositório de referência](https://github.com/mrmariixx/XyCrosshair) · [Projeto Inter](https://github.com/rsms/inter)

## Decisão de stack

WPF oferece renderização vetorial e layout independente de resolução para o editor. Win32 dá controle sobre transparência por pixel, foco, passagem de mouse e geometria física. A implementação usa C# e .NET Framework 4.8, com compilação offline pelo compilador instalado e sem dependências de NuGet. O Framework foi escolhido para produzir e rodar o binário neste Windows. Essa escolha evita a instalação de Qt, Visual Studio ou Electron para o build desta entrega. [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview) · [UpdateLayeredWindow](https://learn.microsoft.com/pt-br/windows/win32/api/winuser/nf-winuser-updatelayeredwindow)

Qt 6 com Qt Quick ofereceria um frontend C++/QML rico e multiplataforma. Exigiria distribuir Qt e observar os requisitos de cada módulo sob LGPL/GPL ou licença comercial. GLFW/OpenGL favorece uma janela gráfica pequena, com custo adicional de construir componentes de edição e acessibilidade ao redor de ImGui. Direct2D/DirectComposition segue como alternativa caso a renderização animada sob carga justifique um compositor próprio. [Licenças Qt](https://doc.qt.io/qt-6/licensing.html) · [Renderização WPF](https://learn.microsoft.com/dotnet/desktop/wpf/graphics-multimedia/wpf-graphics-rendering-overview)

| Arquivo | Responsabilidade |
|---|---|
| Model.cs | Tipos de dados, catálogo, validação, códigos portáteis e persistência. |
| Rendering.cs | Geometria, efeitos, poses, desenhos compartilhados, imagens e exportações. |
| WindowsDesktop.cs | Overlay nativo, atalhos, posição em pixels físicos e executável em primeiro plano. |
| MainWindow.cs e Theme.xaml | Estrutura do estúdio, tokens de UI, estado, navegação, bandeja e temporizadores. |
| Editor.cs e Visuals.cs | Inspetor, canvas, histórico de edição e ajuste de elementos. |
| Pages.cs | Dashboard, biblioteca, perfis, comparação e preferências. |
| SelfTest.cs | Testes de integração e verificação do desktop. |

### Renderização, segurança e compatibilidade

O motor rasteriza a geometria em BGRA pré-multiplicado e copia a área delimitada para uma DIB Win32. O overlay usa uma janela topmost, com transparência por pixel e os estilos transparent e no-activate. O estado estático só redesenha em mudanças; a animação solicitada usa um timer de 30 Hz. O perfil consulta somente o executável da janela em primeiro plano, em intervalos de 500 ms quando necessário. O overlay usa pixels físicos no monitor, inclusive com origem virtual negativa. A interface tem manifesto PerMonitorV2.

O desenho fora do processo é destinado a janelas e ao modo sem bordas. Tela cheia exclusiva fica sem suporte garantido. A investigação do SDK Game Bar identificou que o caminho oficial envolve um widget UWP separado, com manifesto e empacotamento próprios. A compatibilidade depende do jogo, do modo de exibição e das regras do servidor. Não se garante a ausência de sanções. [Xbox Game Bar SDK](https://learn.microsoft.com/en-us/gaming/game-bar/quickstart/introduction)

Não há injeção, captura de frames de jogos, acesso à memória de jogos, hooks de teclado, drivers, tráfego, plugins executáveis ou privilégios elevados. O acesso ao processo em primeiro plano usa somente QUERY_LIMITED_INFORMATION para identificar seu caminho. O PNG exige assinatura e cabeçalho com dimensões até 2048 × 2048. Imagens têm até 1 MiB. A leitura SVG proíbe DTD e resolução de recursos externos e usa um subconjunto vetorial de tamanho e profundidade limitados. O JSON tem limite de 16 MiB, profundidade limitada e rejeição de metadados de tipo. O salvamento usa um arquivo temporário, gravação sincronizada, troca atômica e cópia anterior válida.

### Desempenho

Metas verificáveis da versão inicial: zero redesenhos estáticos em 1,5 s na bandeja, CPU abaixo de 1% de um núcleo nessa janela sem troca automática e quantidade estável de recursos nativos por superfície. Para testes de campo: abertura interativa em até 2 s a quente e duração de desenho animado inferior a 12 ms no percentil 95 em uma mira de 2 camadas. Só a amostra de overlay estático foi medida nesta distribuição. O relatório informa o working set do processo de testes WPF, que também carregou as páginas antes da amostra.

### Referências de produto

| Referência | Leitura aplicada |
|---|---|
| [Linear](https://linear.app/) | Navegação contida, contraste moderado e títulos curtos. |
| [Razer Synapse 4](https://www.razer.com/br-pt/synapse-4) | Perfis como entidade gerenciável com ajustes agrupados. |
| [SteelSeries GG](https://steelseries.com/gg) | Acesso entre ferramentas, biblioteca e configurações em um shell comum. |
| [Elgato Wave Link](https://www.elgato.com/us/en/s/wave-link-app) | Ajustes de precisão com números explícitos e controles visuais. |
| [Crosshair X](https://store.steampowered.com/app/1366800/Crosshair_X/) | Acesso rápido a editor, designs reutilizáveis e biblioteca. |
| [HudSight](https://hudsight.com/) | Uso de imagens para miras e estudo da Game Bar pelo SDK oficial. |
| [CrossOver](https://github.com/lacymorrow/crossover) | Projeto aberto consultado para imagem, atalhos e escolha de posição. |

As referências serviram para avaliar fluxos. A interface e os elementos visuais deste estúdio são originais.

