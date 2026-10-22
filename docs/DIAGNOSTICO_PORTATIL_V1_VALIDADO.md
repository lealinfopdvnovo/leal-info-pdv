# Diagnóstico portátil LEAL INFO 1.0 — validado em Windows

Código validado: 58a22cd81fdab98698d963679f6ecd40c06e2391.
Execução: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39792635661.
30 verificações aprovadas, sem cenários ignorados.

Download portátil Windows x64: https://github.com/lealinfopdvnovo/leal-info-pdv/actions/runs/39792635661/artifacts/12432535365.
O artefato contém LEAL_INFO_DIAGNOSTICO.exe, LEIA-ME.txt e SHA256.txt. Sem setup ou dependência de .NET instalado. A distribuição inclui o runtime e não integra nem modifica o PDV.

Fluxo: abrir com o PDV disponível; escolher processo/pasta; INICIAR ANÁLISE; usar o PDV e marcar etapas; ENCERRAR COLETA; SALVAR RELATÓRIO fora da instalação. Entrega HTML + JSON locais, sem transmissão automática.

Coleta CPU e memória do processo/sistema e do analisador, E/S lógica, TCP estabelecidas por PID, rede global dos adaptadores, hardware/RAM/espaço livre e inventário limitado de metadados. Não atribui bytes de rede ao Firebase/PDV, não mede latência de telas automaticamente, não confirma duplicação por tamanho, não remove arquivos. Veja qa/pdv-diagnostics/README.md.

Testes incluíram contadores Windows reais, ausência de processo, amostra inicial de CPU desconhecida, limitação/cancelamento de inventário, links sem recursão, HTML escapado, exportação HTML/JSON, botões reais de início/parada e proteção de exportação vinculada à pasta efetivamente monitorada. Arquivos-sentinela de banco, licença, configurações e imagens permaneceram com hashes e datas de modificação originais. Sem alterações em instalações, canais de atualização ou código operacional do PDV.

A pasta qa já é excluída pela compilação do PDV. Trabalho isolado em codex/diagnostico-portatil. O diagnóstico no computador do usuário depende de sua execução local; não foi declarado como já realizado.
