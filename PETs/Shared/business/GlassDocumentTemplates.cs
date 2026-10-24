namespace LealInfoPDV;

// Textos iniciais editáveis. Não alteram cadastro, projeto, preço ou financeiro.
internal static class GlassDocumentTemplates
{
    internal static GlassContractClauses Contract(GlassDocumentContext c)
    {
        var p=c.Project;
        return new(
            $"A CONTRATADA fornecerá os materiais e executará os serviços de vidraçaria descritos no projeto {p.Description}, modelo {p.Model}, revisão {c.ProjectRevision}. Serviço: {p.Quote.Service}. As medidas, quantidades e materiais discriminados neste documento integram o escopo e devem ser conferidos pelas partes antes da fabricação.",
            $"O valor comercial do projeto é {GlassBrazilianMoney.Format(c.CommercialValue)}, conforme orçamento vinculado. Os preços e materiais informativos reproduzem o cadastro e não acrescentam valores ao total. Serviços ou materiais adicionais somente serão executados mediante orçamento e aprovação expressa das partes.",
            $"Condição registrada no projeto: {c.FinancialCondition}\r\nForma de pagamento, parcelas e vencimentos: a definir de comum acordo neste campo. Cada valor efetivamente recebido terá seu próprio comprovante. Este contrato não representa recibo nem quitação.",
            "Data de início: a combinar entre as partes. Data de entrega/instalação: a combinar entre as partes. A programação considerará a aprovação do projeto, as condições de pagamento pactuadas e a disponibilidade do local. Qualquer alteração deverá ser comunicada e acordada por escrito, com indicação da nova data.",
            "A fabricação dependerá da conferência técnica das medidas e da aprovação das especificações. A instalação será realizada no endereço acordado, com acesso e condições de segurança adequados. Intervenções de alvenaria, elétrica, pintura ou outros serviços somente integram o escopo quando expressamente descritos no orçamento.",
            "Compete à CONTRATADA executar o serviço conforme as especificações aprovadas, orientar sobre uso e conservação e comunicar impedimentos técnicos. Compete à CONTRATANTE conferir o projeto, disponibilizar o local nas condições acordadas e cumprir o pagamento pactuado. Mudanças de medidas, materiais ou escopo dependerão de concordância das partes, com registro dos efeitos sobre preço e prazo.",
            "As partes declaram ter conferido os dados, o escopo e as condições deste documento. As comunicações e alterações serão registradas por escrito. Permanecem assegurados os direitos legalmente aplicáveis às partes. Observações específicas do projeto: nenhuma adicional informada.")
        {
            Warranty="São preservadas as garantias legais aplicáveis aos produtos e serviços. Eventual garantia contratual adicional será descrita em termo escrito, com cobertura, prazo, forma e local de atendimento, sem substituir a garantia legal. A CONTRATADA entregará as orientações de uso, limpeza e manutenção e atenderá as solicitações pelos contatos indicados neste documento.",
            Signatures=Signatures(c,true)
        };
    }
    internal static string Signatures(GlassDocumentContext c,bool witnesses=false)=>
        "Local e data: "+(string.IsNullOrWhiteSpace(c.Company.CityState)?"________________________":c.Company.CityState)+", ____ / ____ / ________.\r\n\r\n"+
        "________________________________________________\r\nCONTRATADA / EMITENTE: "+c.Company.Name+"\r\nCPF/CNPJ: "+c.Company.Document+"\r\n\r\n"+
        "________________________________________________\r\nCONTRATANTE / CLIENTE: "+c.Customer.Name+"\r\nCPF/CNPJ: "+c.Customer.Document+
        (witnesses?"\r\n\r\nTestemunha 1: ________________________ CPF: __________________\r\nTestemunha 2: ________________________ CPF: __________________":"");
    internal static string ReceiptDeclaration(GlassReceiptSource s)=>
        $"Recebemos de {s.Context.Customer.Name}, CPF/CNPJ {s.Context.Customer.Document}, a importância de {GlassBrazilianMoney.Format(s.Amount)}, referente ao projeto {s.Context.Project.Description}, conforme o recebimento identificado neste documento. A declaração limita-se ao valor deste lançamento; não presume quitação integral do projeto nem de outras obrigações.";
    internal static string Revision(GlassDocumentContext c)=>
        $"Esta revisão documenta as especificações do projeto {c.Project.Description}, revisão {c.ProjectRevision}, reproduzidas acima para conferência das partes.\r\n\r\nMOTIVO E DESCRIÇÃO DA REVISÃO\r\nConferência e aprovação das medidas, materiais, quantidades e condições do projeto apresentado. Ajustes específicos acordados: nenhum adicional informado.\r\n\r\nEFEITOS SOBRE VALOR E PRAZO\r\nValor comercial de referência: {GlassBrazilianMoney.Format(c.CommercialValue)}. Este é o valor de referência para a conferência das partes. Eventuais diferenças de preço, forma de pagamento ou prazo deverão ser descritas e aprovadas pelas partes antes da execução. Prazo confirmado para esta revisão: a combinar.\r\n\r\nAPROVAÇÃO\r\nAs partes conferem as informações e registram sua concordância com esta revisão. A aprovação desta revisão não constitui recibo de pagamento. Fabricar ou instalar somente conforme as especificações efetivamente aprovadas. Permanecem válidas as demais condições contratuais não alteradas por acordo expresso.";
}
