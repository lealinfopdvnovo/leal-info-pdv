# LEAL INFO PDV — GLOBAL e PETs

**GLOBAL é o pai; 001, 002, 003 e 004 são perfis filhos independentes.**

- `GLOBAL/`: código comum existente, único e referenciado pelos quatro perfis.
- `PETs/001/`, `PETs/002/`, `PETs/003/`, `PETs/004/`: projetos, identidades e versões próprios.
- `PETs/Shared/`: módulos de perfil já existentes e reutilizados, sem copiar o GLOBAL.
- `organization/lineage.lock.json`: origem e hashes de cada arquivo preservado; referências publicadas registradas sem alterá-las.
- `archive/workflows/`: rotinas históricas mantidas, sem disparar publicação nesta branch.

Apenas diretórios e caminhos dos projetos foram reorganizados. A base comum reaproveitada foi validada em `39868216661`. As referências operacionais mais recentes permanecem registradas em cada perfil; suas instalações e canais não foram substituídos.

**Nenhuma publicação, migração, instalação ou alteração de produção. A organização não equivale à homologação de uma migração das versões operacionais atuais.**

Verificação rápida: `python organization/verify.py`. Compilação de cada filho: `dotnet build PETs/001/Pet.csproj` (e equivalentes). As verificações Windows aplicam as mesmas etapas de preparação já utilizadas pela consolidação original.
