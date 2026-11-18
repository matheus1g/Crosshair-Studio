# Design system

As cores vêm do recurso **src/Theme.xaml**. O tema claro troca os recursos de nível de janela e preserva a hierarquia. O Playground controla seu próprio fundo para manter uma comparação previsível.

| Token | Escuro | Claro | Uso |
|---|---|---|---|
| Base | #0B0D12 | #EDF0F7 | Quadro de trabalho e campos |
| Sidebar | #10121A | #F4F5FA | Navegação e barra de título |
| Panel | #141720 | #FFFFFF | Painéis de conteúdo |
| Raised | #1B1F2A | #EBEDF6 | Botões e controles |
| Line | #282C3B | #D4D8E6 | Separadores e bordas |
| Text | #F4F3FA | #17192A | Texto principal |
| Muted | #9DA3B8 | #545A71 | Rótulos e metadados |
| Accent | #A78BFA | #6534C4 | Foco, seleção e controles |
| Primary | #7045D5 | #7045D5 | Ação principal com texto branco |
| Cyan | #65EBF8 | #00728B | Dados de preview |
| On | #77E9AB | #1D7444 | Estado de overlay ativo |

- **Tipografia** usa Inter para interface e Consolas para atalhos, coordenadas, versão e métricas. Títulos de página de 31 DIP, painéis de 15–17, corpo de 13–14 e rótulos de 10–12.
- **Espaçamento** usa separações finas de 4–8 DIP, 11–16 entre componentes e aproximadamente 24–28 em margens de página.
- **Raios** de 7–8 DIP em controles, 10 no canvas, 12 nos painéis e cantos totalmente arredondados nos indicadores de alternância.
- **Estados** mostram foco violeta visível, atenuação em controles desabilitados, realce ao passar o mouse e fundo violeta atenuado na navegação selecionada. O estado do overlay tem texto e marcador.
- **Movimento** usa transição de entrada de 105 ms entre páginas. O modo desempenho suspende a transição e os temporizadores de animação visual.
- **Componentes** incluem navegação lateral, indicador de overlay interativo, painel, cartão de preset, opções suspensas, alternância, slider com campo numérico, feedback e canvas.
- **Acessibilidade** oferece nomes para controles com ícones, navegação por teclado, foco visível, atalhos ajustáveis e controle de escala de interface. Miras e canvas preservam seus próprios parâmetros de cor.

