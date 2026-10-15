# ANGRA VIDROS 0.1.5 — teste real

Motor: InstructPix2Pix no Space original público, API Gradio, com cota gratuita compartilhada. Sem OpenAI pago, cartão ou fallback cobrado. Serviço HTTPS: https://angra-vidros-gateway.classyglobe9.chatgpt.site . Retorna nova imagem derivada da fotografia enviada; não é desenho técnico nem define medidas de fabricação. Limite adicional: 4 tentativas/dia por aparelho; a cota do provedor pode encerrar antes.

A primeira utilização exige autorização do celular pelo proprietário do serviço, usando login Sites/ChatGPT. Nenhuma chave de API, senha, endereço técnico ou código de conexão é digitado no app. Chave privada EC gerada no AndroidKeyStore; somente chave pública sai do aparelho. Requisições assinadas vinculam corpo, data e nonce. Servidor não persiste fotografias; o cache do provedor segue a política dele.

## Preservação da instalação aprovada

A 0.1.4 entregue foi assinada com certificado SHA256 771db7f3bbcd76b97f3ad1558a69c89a4b72de6a6e1d0da5337e6418e16b6bf1. Um novo build debug apresentou outro certificado (209976d6efb1f9cacd0f05e778e4e12507ca36c46eb3fb9df56c5d875ac46beb); a chave privada anterior não está disponível. Por isso este APK debug usa somente o sufixo de instalação .teste. O app aprovado e seus projetos ficam intactos; os rascunhos antigos não são migrados para a instalação de teste. Não desinstalar o aprovado. Namespace e applicationId de release permanecem originais.

O CI instala o APK 0.1.4 de hash homologado e verifica que permanece instalado com arquivo marcador depois dos testes na variante separada. Splash, arte, animações, medidas e interface principal preservados. Apenas transporte/autorização adaptados. PDV e canais não são tocados.

O teste ponta a ponta usa fotografia pública real de Park taeho, CC BY-SA 4.0, identificada em app/src/androidTest/assets/ATTRIBUTION.txt; não utiliza foto privada de cliente. Aprovação técnica em emulador não é homologação física.
