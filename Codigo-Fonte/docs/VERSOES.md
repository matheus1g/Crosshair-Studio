# Histórico dos marcos

A distribuição 0.3.2 combina os três marcos em um executável Windows x64.

### Atualização 0.3.2

- Vários jogos e aplicativos podem compartilhar um perfil. O seletor permite adicionar executáveis em lote, e o executável salvo nas versões anteriores é migrado para a lista.
- O cartão **Perfil em uso** explica o modo automático e indica quando está usando o perfil selecionado manualmente por falta de correspondência.
- O fundo do hover e do clique cobre o botão inteiro, inclusive nos itens selecionados da barra lateral.

### Atualização 0.3.1

- Mais espaço entre os cartões do Editor e painel de propriedades embaixo do canvas nas janelas estreitas.
- Ações **Copiar código** e **Colar código** disponíveis na barra do Editor. O formato compacto **CSX-** usa blocos de cinco caracteres e continua aceitando importações legadas **CS1-**.
- Seleção manual e automática de perfil direto da **Visão geral**, no cartão **Perfil em uso**.

| Marco | Implementado |
|---|---|
| V1 · Núcleo funcional | Overlay Win32 transparente, sempre no topo, click-through, centralização, editor ao vivo, presets originais, atalhos configuráveis, bandeja, salvamento local, diagnóstico e recuperação de configurações. |
| V2 · Personalização avançada | Até 32 camadas por mira. Formas vetoriais, PNG e subconjunto SVG, nomes, grupos, visibilidade, bloqueio, duplicação e ordem de camadas. Biblioteca com 16 presets, busca, categorias, favoritos e prévia independente. Perfis por executável, identificação da janela em primeiro plano, modo manual, exceções, modelos, posição, escala, monitor e suspensão por perfil. Importação JSON, exportação JSON/PNG/SVG, backup e restauração. |
| V3 · Experiência premium | Pulsação, fade, giro, variação de tamanho e keyframes, com duração, loop, prévia e ativação por perfil. Gradiente linear, contorno, borda interna, glow, sombra e suavização em passos. Grade, snap, ajuste por teclado e mouse, fundos locais, simulação de resolução, sugestão de contraste, comparação, códigos CSX com leitura de CS1, histórico, seleção de monitor, tema claro/escuro, idioma, escala e modo desempenho. |

## Verificação

- O teste confirmou a mira composta sobre uma janela de outra classe, a cor no centro, a transparência nos cantos, prioridade de desenho, passagem da seleção no centro e ausência de captura de foco.
- Testes cobrem editor, histórico, temas e navegação, persistência e recuperação, correspondência de executáveis, compartilhamento, keyframes, validação de PNG/SVG e exportação. A suíte também confirma que editar a mira ativa redesenha o overlay e que salvar persiste a mudança.
- O Windows registrou os atalhos no teste de desktop, rejeitou uma combinação já reservada e aceitou o registro após a liberação. O teste não envia teclas ao jogo. Cada falha de registro aparece em **Configurações**.
- A posição em coordenadas negativas foi coberta por teste. Monitores físicos com escalas diferentes, passagem de DPI entre monitores, instalação registrada no menu Iniciar e inicialização automática precisam de validação numa sessão Windows comum.
- A instalação, reinstalação e remoção dos arquivos foram testadas em uma pasta isolada. As mudanças reais no registro do Windows e menu Iniciar exigem abrir o instalador como usuário.

## Escopo ainda aberto

- Widget Xbox Game Bar, avaliação em jogos e tela cheia exclusiva.
- Atualização opcional via canal oficial e galeria de presets da comunidade. A versão atual é totalmente local e compartilha códigos diretamente.
- Ampliação da compatibilidade SVG, incluindo gradientes importados, filtros, máscaras, CSS, fontes e round-trip geral. O importador aceita formas, caminhos, grupos e transformações simples. A exportação gera SVG estático de camadas vetoriais, com curvas convertidas em segmentos e gradiente embutido. Use JSON para reproduzir a edição.
- Desfoque gaussiano e efeitos raster por camada, guias de alinhamento entre múltiplas camadas e transformação conjunta de grupos. A suavização atual usa contornos graduais e os grupos permitem organização e visibilidade conjunta.
- Revisão completa da localização em inglês para mensagens de validação, dados iniciais e instalador. A interface principal e os controles oferecem os dois idiomas.
- Testes com configurações máximas, múltiplos modelos de GPU e diferentes versões do Windows 10/11. Os testes do pacote executam na máquina disponível.

