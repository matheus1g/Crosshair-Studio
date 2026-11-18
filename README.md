Crosshair Studio
Editor de miras em camadas e overlay para Windows, com prévia em tempo real, perfis por jogo e compartilhamento de configurações.

Windows 10/11 x64 · C# · WPF + Win32 · MIT · Versão 0.3.2

O Crosshair Studio permite criar e organizar miras para usar sobre janelas compatíveis. Combine formas, ajuste cores e efeitos, associe a mesma configuração a vários jogos e alterne entre favoritos. A operação é local e funciona offline.
Recursos
Área	Disponível na versão 0.3.2
Overlay	Transparência por pixel, passagem dos cliques, janela sempre no topo, centralização e ajuste de posição.
Editor	Até 32 camadas, formas vetoriais, PNG, SVG básico, duplicação, ordenação, bloqueio, grupos e desfazer/refazer.
Aparência	Cores HEX/RGB/RGBA, gradiente linear, opacidade, contornos, borda interna, glow, sombra, suavização, rotação e espelhamento.
Biblioteca	16 presets originais, busca, categorias, favoritos, duplicação e histórico de revisões.
Perfis	Vários executáveis por perfil, seleção manual ou automática, exceções e ajustes de monitor, posição, escala e animação.
Playground	Fundos de laboratório, claro, escuro ou PNG local, zoom, grade, snap, simulação de resolução e sugestão de contraste.
Comparação	Duas miras lado a lado com o mesmo fundo e zoom.
Animações	Pulsação, fade, giro, variação de tamanho e keyframes com duração e repetição.
Compartilhamento	Códigos compactos CSX, leitura de códigos CS1, importação/exportação JSON, PNG transparente e SVG vetorial.
Aplicativo	Atalhos globais configuráveis, bandeja, temas claro/escuro, escala da interface, português e inglês.
Dados	Salvamento local, cópia de recuperação, exportação/restauração de backup e diagnóstico local.


Instalação
O aplicativo requer Windows 10/11 x64 e .NET Framework 4.8. O pacote distribuído contém as pastas Portatil, Instalador, Codigo-Fonte e Verificacao.
Versão portátil
1. Extraia o arquivo ZIP por completo.
2. Abra Portatil/CrosshairStudio.exe. Mantenha as pastas fonts e userdata ao lado do executável.
3. Em Visão geral, clique em Ativar mira.
4. Escolha um preset na Biblioteca ou abra o Editor para criar sua mira.
5. Use o jogo em modo janela ou janela sem bordas.
Por padrão, fechar ou minimizar a janela recolhe o aplicativo para a bandeja. Para encerrar, clique com o botão direito no ícone e selecione Sair.
Instalação por usuário e atualização
Execute Instalador/Instalar.cmd. A instalação cria um atalho no menu Iniciar e registra o programa em Aplicativos instalados do Windows. O instalador e o executável são distribuídos sem assinatura digital. A desinstalação preserva as configurações do usuário.

Encerre a versão anterior pela bandeja antes de atualizar. No modo portátil, copie o conteúdo da nova pasta Portatil para a pasta atual e mantenha userdata, que contém suas miras e perfis. Na versão instalada, execute o novo instalador.
Usando o editor
Selecione uma camada no painel à esquerda e ajuste suas propriedades à direita. As camadas no topo da lista são desenhadas por último. Ao editar a mira ativa, a alteração aparece no overlay em tempo real. Clique em Salvar mira para gravar e aplicar o resultado ao perfil em uso.
- Adicionar um ponto — use + Adicionar → Ponto e ajuste tamanho e cor.
- Remover uma camada — selecione-a e clique em ×. Desative Visível para apenas ocultá-la. Desbloqueie a camada antes de removê-la.
- Mover um elemento — arraste no canvas ou dê foco ao canvas e use as setas. O Snap usa passos de 4 px.
- Editar números — use ponto ou vírgula decimal e pressione Enter para confirmar.
- Desfazer, refazer e salvar — use Ctrl+Z, Ctrl+Y e Ctrl+S fora dos campos de texto.
As cores aceitam #RRGGBB, #RRGGBBAA, rgb(r,g,b) e rgba(r,g,b,a). O campo Gradiente opcional define a segunda cor do gradiente.
Importar e compartilhar
No Editor, Copiar código gera um código da mira em edição. Colar código abre a importação e permite aplicar a mira recebida. O formato atual começa com CSX-, usa blocos de cinco caracteres e inclui verificação de integridade. A importação continua aceitando os códigos anteriores CS1-.

O comprimento varia com a quantidade de camadas, propriedades, animações e imagens. Para miras com imagens grandes, use um arquivo JSON. Os códigos CSX e CS1 são formatos próprios do Crosshair Studio.
Formato	Uso
CSX	Compartilhar a configuração da mira por texto.
JSON	Preservar camadas, imagens e parâmetros editáveis.
PNG	Exportar uma imagem com fundo transparente.
SVG	Exportar um desenho estático composto por camadas vetoriais.


Importar JSON carrega configurações. + PNG ou SVG adiciona uma imagem como camada. Exportar arquivo oferece JSON, PNG e SVG.
Perfis de jogos e aplicativos
Em Perfis de jogo, crie um perfil, escolha a mira e adicione executáveis em Jogos e aplicativos associados. Você pode digitar um nome como jogo.exe, informar o caminho completo ou selecionar vários arquivos em Procurar executáveis….

Cada perfil permite definir monitor, posição, escala, permissão de exibição e animação. A opção Mira só nos apps da lista restringe sua exibição aos executáveis associados. As associações individuais das versões anteriores são migradas para a lista.
Modo automático
Em Visão geral → Perfil em uso, selecione Automático. O aplicativo identifica o executável da janela em primeiro plano e usa o perfil correspondente.
Situação	Comportamento
O executável corresponde a um perfil	Aplica a mira e as configurações desse perfil.
Nenhum perfil corresponde	Usa o último perfil selecionado manualmente.
Mais de um perfil corresponde	Usa o primeiro na lista de perfis.
Um executável da lista de exceções está em primeiro plano	Oculta a mira.
Você escolhe um perfil pelo nome	Ativa a seleção manual.


Por exemplo, um perfil FPS pode ser associado a jogo-a.exe e jogo-b.exe, compartilhando a mesma mira nos dois jogos.
Atalhos
Ação	Combinação inicial
Mostrar ou ocultar o overlay	Ctrl+Alt+F8
Próxima mira favorita	Ctrl+Alt+F9
Mira favorita anterior	Ctrl+Alt+F7
Abrir o editor	Ctrl+Alt+F10
Centralizar a mira	Ctrl+Alt+F6
Alternar monitor	Ctrl+Alt+F5
Alternar perfil	Ctrl+Alt+F4
Ativar ou pausar animações do overlay	Ctrl+Alt+F11


Personalize as combinações em Configurações. A aplicação informa quando o Windows recusa um atalho. A seleção rápida usa os favoritos e, quando a lista de favoritos está vazia, percorre a biblioteca.
Compilação e testes
Abra o Windows PowerShell na raiz do código-fonte, onde estão build.ps1, src e assets. No ZIP distribuído, essa raiz é a pasta Codigo-Fonte.

O build usa o compilador C# e as bibliotecas do .NET Framework instalados no Windows. Os scripts compilam para x64 e usam as fontes Inter incluídas no projeto.
# Compilar
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1

# Compilar e executar testes de dados e renderização
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test

# Incluir testes com janelas e overlay no desktop
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -DesktopTest

# Gerar o pacote de distribuição e executar os testes de desktop
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1 -DesktopTest
O executável é gerado em build/CrosshairStudio.exe e o relatório em build/tests.txt. O pacote ZIP é criado em outputs, na pasta pai da raiz do projeto.

Os testes de desktop abrem janelas temporárias e exigem uma sessão gráfica do Windows. A suíte cobre persistência e recuperação, migração de perfis, múltiplos executáveis, compartilhamento, importação/exportação, editor, atalhos e comportamento do overlay. O empacotamento verifica instalação, reinstalação e remoção dos arquivos em uma pasta isolada.
Estrutura do código
Caminho	Responsabilidade
src/Model.cs	Modelos de dados, presets, validação e persistência.
src/Rendering.cs	Formas, efeitos, animações, importação de imagens e exportação.
src/WindowsDesktop.cs	Overlay Win32, atalhos globais e identificação do app em primeiro plano.
src/MainWindow.cs	Janela principal, estado, navegação e integração com a bandeja.
src/Editor.cs	Camadas, propriedades, ferramentas de edição e desfazer/refazer.
src/Pages.cs	Visão geral, biblioteca, perfis, comparação e configurações.
src/ShareCode.cs	Codificação e leitura de códigos de compartilhamento.
src/Theme.xaml e src/Visuals.cs	Componentes visuais, temas, ícones e canvas.
src/SelfTest.cs	Testes automatizados e verificações de desktop.
assets/	Fontes Inter e ícone do aplicativo.
packaging/	Scripts de instalação por usuário.
docs/	Arquitetura, design system e registro dos marcos.


Compatibilidade, dados e privacidade
O overlay usa uma janela Win32 transparente e não ativável. A identificação de perfis consulta o caminho do executável em primeiro plano. A operação é local, sem telemetria, conexão obrigatória ou acesso a credenciais. O aplicativo desenha sua própria janela sem injetar DLLs ou ler a memória dos jogos.
Modo de execução	Local das configurações
Portátil	Pasta userdata ao lado do executável.
Instalado	%LocalAppData%\Crosshair Studio.


Em Configurações, use Exportar backup e Restaurar backup para transferir seus dados. O arquivo de diagnóstico registra horário, operação e tipo de exceção.

A compatibilidade foi direcionada a janelas e ao modo sem bordas. Tela cheia exclusiva e integração com Xbox Game Bar fazem parte do escopo pendente. Cada jogo possui regras próprias sobre overlays, e a utilização deve respeitá-las. Não há garantia de ausência de sanções.

PNG e SVG têm limite de 1 MiB por imagem. PNG aceita até 2048 × 2048 pixels. O importador SVG aceita formas, caminhos, grupos e transformações simples. A compatibilidade com CSS, filtros, máscaras, fontes e gradientes importados ainda precisa ser ampliada.
Marcos e próximos passos
A versão 0.3.2 reúne funcionalidades dos três marcos de desenvolvimento. O registro detalha os recursos implementados, a validação realizada e as pendências. [Histórico de versões](docs/VERSOES.md)
Marco	Entregas presentes
V1 — Núcleo funcional	Overlay, editor, presets, atalhos, bandeja e salvamento.
V2 — Personalização avançada	Camadas, PNG/SVG básico, biblioteca, perfis e importação/exportação.
V3 — Experiência premium	Animações, efeitos, Playground, comparação, compartilhamento, histórico e temas.


O escopo final inclui ampliar o suporte SVG, evoluir o editor de grupos e alinhamento, revisar a localização em inglês, criar um canal opcional de atualizações e uma galeria comunitária, investigar a integração oficial com Xbox Game Bar e expandir os testes em jogos, GPUs e monitores com DPI misto.

As decisões de implementação e os componentes visuais estão documentados no projeto. [Arquitetura](docs/ARQUITETURA.md) · [Design system](docs/DESIGN-SYSTEM.md)
Contribuir
Para relatar um problema, informe a versão do aplicativo, a versão do Windows, os passos para reproduzir e o resultado esperado. Se envolver o overlay, inclua o modo de exibição do jogo, a resolução e a escala dos monitores. Para problemas com uma mira, anexe seu código ou JSON.

Alterações podem ser propostas por pull request, acompanhadas de uma descrição do comportamento e da verificação realizada.
Licença
O código é distribuído sob licença MIT. As fontes Inter incluídas no projeto usam SIL Open Font License 1.1 e mantêm os créditos dos autores
