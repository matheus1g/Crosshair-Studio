# Crosshair Studio 0.3.2

Editor de miras em camadas e overlay para Windows 10/11 x64. Esta distribuição reúne o núcleo da V1 e funcionalidades integradas das V2 e V3.

## Abrir pela primeira vez

1. Extraia todo o ZIP. Para usar o modo portátil, abra **Portatil/CrosshairStudio.exe**. Mantenha as pastas **fonts** e **userdata** ao lado do executável.
2. Na tela **Visão geral**, clique em **Ativar mira**. A mira aparece no centro do monitor do perfil.
3. Abra **Editor**, escolha uma camada e altere tamanho, cor ou posição. A edição da mira que está ativa é refletida ao vivo. **Salvar mira** grava a configuração e aplica a mira atual ao perfil.
4. No jogo, use o modo **janela** ou **janela sem bordas**. Consulte as regras do jogo sobre sobreposições de terceiros.
5. Para encerrar, selecione **Sair** no ícone da bandeja. Fechar a janela a recolhe para a bandeja por padrão.

Para instalar, execute **Instalador/Instalar.cmd**. O instalador registra o aplicativo para o usuário atual em **Aplicativos instalados** e cria um atalho no menu Iniciar. A desinstalação preserva a biblioteca e os perfis. O aplicativo e o instalador por script são distribuídos sem assinatura digital.

Para atualizar a versão portátil, encerre o estúdio pela bandeja e extraia o conteúdo de **Portatil** na pasta portátil atual, mantendo a pasta **userdata**.

## Editor e perfis

- As camadas superiores na lista são desenhadas por último. Para mover uma camada, arraste no canvas ou use as setas após dar foco ao canvas. Com Snap, os deslocamentos usam passos de 4 px. Use **Ctrl+Z**, **Ctrl+Y** e **Ctrl+S** fora de campos de texto.
- Use HEX no formato **#RRGGBB** ou **#RRGGBBAA**, ou os formatos **rgb(…)** e **rgba(…)**. No formato RGBA funcional, o alfa aceita 0–1 ou valores inteiros de 2–255.
- Os campos numéricos aceitam vírgula ou ponto decimal. Pressione Enter para confirmar. Em campos de várias linhas com edição imediata, use Ctrl+Enter ou transfira o foco.
- O Playground compara fundos de laboratório, escuro, claro e PNG local. Selecione uma resolução para simular os pixels da saída e ajuste o zoom.
- Adicione jogos e aplicativos em **Perfis de jogo → Jogos e aplicativos associados**. Digite um nome como **jogo.exe** ou selecione vários arquivos em **Procurar executáveis…**. As associações anteriores aparecem automaticamente nessa lista.
- Em **Visão geral → Perfil em uso**, escolha **Automático** para aplicar o perfil associado ao executável que estiver em primeiro plano. Se nenhum perfil corresponder, o estúdio volta ao último perfil selecionado manualmente. Se mais de um corresponder, vale o primeiro da lista de perfis. Escolher um perfil pelo nome ativa o modo manual.
- Para restringir o overlay de um perfil a seus executáveis, marque **Mira só nos apps da lista**.
- No **Editor**, use **Copiar código** para compartilhar a mira que está editando e **Colar código** para importar e aplicar. O formato compacto começa com **CSX-** e tem blocos de cinco caracteres. O aplicativo também importa códigos anteriores **CS1-**. Códigos de jogos como CS2 ou Valorant usam formatos próprios.
- O editor importa JSON em **Importar JSON** e imagens em **+ PNG ou SVG**. Use JSON para manter camadas editáveis e compartilhar miras com imagens grandes, PNG para imagem transparente e SVG para saída de camadas vetoriais.

| Ação | Atalho inicial |
|---|---|
| Mostrar ou ocultar | Ctrl+Alt+F8 |
| Próxima mira favorita | Ctrl+Alt+F9 |
| Mira favorita anterior | Ctrl+Alt+F7 |
| Abrir editor | Ctrl+Alt+F10 |
| Centralizar | Ctrl+Alt+F6 |
| Alternar monitor | Ctrl+Alt+F5 |
| Alternar perfil | Ctrl+Alt+F4 |
| Alternar animação | Ctrl+Alt+F11 |

Os atalhos são editáveis em **Configurações**. Use letras, números ou teclas de função com Ctrl/Alt. A tela sinaliza combinações recusadas pelo Windows.

## Compilar e testar

Em **Codigo-Fonte**, use Windows PowerShell e .NET Framework 4.8 no Windows x64. O Windows já inclui o compilador e as bibliotecas usadas no build. As fontes Inter são fornecidas no código-fonte.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
    powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -DesktopTest
    powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1 -DesktopTest

O executável sai em **build/CrosshairStudio.exe**. O teste de desktop abre janelas temporárias para verificar o desenho e o foco. **Verificacao/Testes.txt** contém o resultado desta compilação. As imagens em **Capturas** vêm do aplicativo em execução.

## Dados e licença

Na instalação, os dados ficam em **%LocalAppData%\Crosshair Studio**. No portátil, ficam em **userdata**. Os logs locais registram somente horário, operação e tipo de exceção. O aplicativo funciona offline. A licença do código é MIT. A fonte Inter é distribuída sob SIL OFL 1.1.

- [Marcos, verificação e pendências](docs/VERSOES.md)
- [Arquitetura, pesquisa e compatibilidade](docs/ARQUITETURA.md)
- [Design system](docs/DESIGN-SYSTEM.md)

No pacote ZIP, esses três documentos ficam em **Codigo-Fonte/docs**.

