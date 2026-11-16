# Loja virtual integrada ao LEAL INFO PDV

**Cópia de referência para centralização das ideias futuras.**
**Origem preservada:** `bau-de-ouro/solicitacoes-pendentes/loja-virtual-integrada-pdv/IDEIA_FUTURA.md`
**Situação:** somente planejamento; não implementar nem publicar sem autorização expressa. GLOBAL e PETs intocados.

---

# Ideia futura — Loja virtual integrada ao Leal Info PDV

Status: **proposta para avaliação futura; não implementar nem publicar sem autorização específica**.
Data: 2026-11-16.

## Visão
Gerador de lojas virtuais pré-prontas dentro do PDV. O próprio comerciante escolhe um modelo, configura marca, cores e logo, publica o site e administra produtos, imagens, preços e promoções sem depender do desenvolvedor. Considerar mensalidade recorrente pelo serviço, com preços a definir.

## Planos propostos
- **Standard:** sem módulo de loja virtual.
- **Plus:** criação e gestão autônoma de loja virtual; catálogo sincronizado com o PDV; pedidos do site recebidos no PDV; controle de estoque e prevenção de duplicidade de vendas.
- **Pro:** tudo do Plus, mais conectores para Mercado Livre, Shopee, Facebook e Instagram, conforme permissões, APIs e elegibilidade das contas; centralização de pedidos, anúncios e estoque quando tecnicamente possível.

## Requisitos para estudo
Isolamento por cliente e loja, autenticação e permissões, domínio/hospedagem, cobrança recorrente e suspensão segura, checkout com provedor de pagamentos, webhooks e reprocessamento idempotente, estoque consistente, logs, privacidade/LGPD, custos operacionais, limites e aprovação de integrações de terceiros. A conexão com redes sociais não garante publicação automática ou checkout nativo.

## Proteções
Não alterar o GLOBAL, PETs, licenças, versões, banco de produção ou funcionalidades homologadas sem autorização expressa. Planejar e testar separadamente antes de qualquer implantação. Esta anotação não autoriza desenvolvimento nem atualização do PDV.
