# LIA — Casa do Adriano: candidata isolada

A receita publicada de login 10.457 recompõe CASA a partir de 874bc1e e aplica o controle LIA somente aos canais 002/004. A base CASA não contém btnAssistenteAI. O GLOBAL recomposto usa o mesmo conjunto histórico de scripts para o botão, estados e ciclo de vida. Esta candidata reaplica esses três scripts somente à cópia CASA, depois da receita exata de login.

Não modifica GLOBAL, PET V1.1, banco, licença, chaves, módulo iFood ou feeds. A visibilidade continua condicionada a InstallationLicense.HasLia. Nenhum executável LIA é substituído. A existência de LIC-AI/LicAi.exe e o áudio real no computador ainda precisam de validação.

O workflow lia_casa_validar.yml compila em Windows, preserva testes de login/rollback e verifica existência única do controle, interação, limites e ocultação no perfil isolado sem licença. Não chama API ou microfone. Os hashes de fontes limitam a restauração adicional a MainForm.cs. O pacote é candidato, sem publicação e identificado como 10.458; não deve ser instalado como atualização sem uma etapa posterior de versionamento/distribuição validada e autorizada.
