# Notificador Windows — preparação Casa do Leal

Candidata 10.447, posterior à Consulta 10.446. Não autoriza publicação GLOBAL.

Componente Win32 nativo, sem uma segunda instância operacional do PDV nem runtime .NET próprio. O binário está incorporado no DLL comum e é extraído após login para a área do usuário em uma pasta identificada pelo SHA-256. Registro apenas no início automático do usuário atual, sem serviço do Windows, setup ou permissões administrativas adicionais. Pode desativar a inicialização pelo menu do ícone.

Consulta HTTPS inicial após 15 segundos; normalmente a cada 30 minutos. Erros de rede produzem espera progressiva, sem janelas de erro. Compara a versão do arquivo instalado, a identidade configurada pelo PDV e o contrato GLOBAL/API 1 dos canais comum e direcionado. Notificações automáticas repetidas da mesma versão/hash ficam suprimidas em preferências próprias. Reinício do Explorer recria o ícone.

Não abre banco, não lê licença, não altera estoque/financeiro e não baixa nem instala atualizações. O clique encaminha ao atualizador do PDV. PDV fechado: abre o programa e exige o login habitual antes da central. PDV aberto com vendas/outra janela: solicita apenas a checagem silenciosa, sem fechar ou focar o caixa. A atualização efetiva continua validada pelo atualizador transacional existente. O agente tem nome, processo e executável próprios; não bloqueia a troca dos executáveis do PDV.

Inclui correção do aviso interno: a janela se oculta antes de iniciar a transação, evitando que a proteção interprete o próprio aviso como outra janela de operação aberta.

Testes isolados: parser/versão/identidade, persistência do aviso, extração e integridade, registro restrito a chave de teste, preferência de inicialização, execução real do agente com PDV fechado, instância única, consumo de memória medido, saída limpa e recuperação do pacote comum. Não usa banco ou licença reais. Bandeja visível, notificação após reinício real, rede real e clique durante venda precisam de homologação no computador da Casa do Leal. Preparação não equivale a publicação ou instalação.

O empacotamento deve compilar o nativo com MSVC e passar NotifierPayload ao build do PDV. Mantém os três arquivos comuns do pacote; o PET permanece separado. O ícone pode ficar no menu de ícones ocultos do Windows; a posição junto ao relógio é controlada pelo usuário/Windows.
