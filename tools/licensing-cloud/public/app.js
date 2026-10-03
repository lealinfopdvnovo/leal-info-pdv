import { initializeApp } from "https://www.gstatic.com/firebasejs/12.19.0/firebase-app.js";
import { getAuth, createUserWithEmailAndPassword, signInWithEmailAndPassword, sendEmailVerification, onAuthStateChanged, signOut } from "https://www.gstatic.com/firebasejs/12.19.0/firebase-auth.js";
import { getFirestore, collection, doc, getDoc, getDocs, query, orderBy, limit, runTransaction, setDoc, serverTimestamp } from "https://www.gstatic.com/firebasejs/12.19.0/firebase-firestore.js";

const config = window.FIREBASE_CONFIG;
const $ = id => document.getElementById(id);
const notice = $("notice");
let auth, db, user = null, privateKey = null, clients = [], keyLockTimer = null;

function message(text, ok = false) {
  notice.textContent = text;
  notice.className = ok ? "notice ok" : "notice";
  notice.hidden = !text;
}
function explainError(error) {
  if (error?.code === "permission-denied") return "A conta entrou, mas não está autorizada nas regras do Firestore. Confira o e-mail proprietário e publique as regras.";
  if (error?.code === "auth/invalid-credential") return "E-mail ou senha incorretos.";
  if (error?.code === "auth/email-already-in-use") return "Este e-mail já tem acesso. Use ENTRAR.";
  if (error?.code === "auth/weak-password") return "A senha da conta precisa ter pelo menos 8 caracteres.";
  if (error?.code === "auth/too-many-requests") return "Muitas tentativas. Aguarde um pouco e tente novamente.";
  return error?.message || "Não foi possível concluir a operação.";
}
function bytesToBase64(bytes) { let s=""; for (const b of bytes) s += String.fromCharCode(b); return btoa(s); }
function base64ToBytes(text) { return Uint8Array.from(atob(text), c => c.charCodeAt(0)); }
function pemBytes(pem, label) {
  const body = pem.replace(`-----BEGIN ${label}-----`, "").replace(`-----END ${label}-----`, "").replace(/\s/g, "");
  return base64ToBytes(body);
}
function concatBytes(...parts) {
  const size = parts.reduce((total, part) => total + part.length, 0);
  const result = new Uint8Array(size); let offset = 0;
  for (const part of parts) { result.set(part, offset); offset += part.length; }
  return result;
}
function derLength(length) {
  if (length < 128) return Uint8Array.of(length);
  const bytes=[]; for (let value=length; value>0; value=Math.floor(value/256)) bytes.unshift(value & 255);
  return Uint8Array.from([0x80 | bytes.length, ...bytes]);
}
function derWrap(tag, body) { return concatBytes(Uint8Array.of(tag), derLength(body.length), body); }
function privateKeyBytes(pem) {
  if (pem.includes("-----BEGIN PRIVATE KEY-----")) return pemBytes(pem, "PRIVATE KEY");
  if (pem.includes("-----BEGIN RSA PRIVATE KEY-----")) {
    const pkcs1 = pemBytes(pem, "RSA PRIVATE KEY");
    const rsaAlgorithm = Uint8Array.from([0x30,0x0d,0x06,0x09,0x2a,0x86,0x48,0x86,0xf7,0x0d,0x01,0x01,0x01,0x05,0x00]);
    return derWrap(0x30, concatBytes(Uint8Array.from([0x02,0x01,0x00]), rsaAlgorithm, derWrap(0x04, pkcs1)));
  }
  throw new Error("A chave precisa ser um PEM RSA não criptografado (PRIVATE KEY ou RSA PRIVATE KEY).");
}

if (!config || !config.apiKey || config.apiKey === "PREENCHER" || !config.projectId || config.projectId === "PREENCHER") {
  $("auth-panel").hidden = false;
  message("Falta configurar public/firebase-config.js com os dados do seu projeto Firebase.");
} else {
  const app = initializeApp(config);
  auth = getAuth(app);
  db = getFirestore(app);
  wireAuth();
}

function wireAuth() {
  let createMode = false;
  $("signup-toggle").addEventListener("click", () => {
    createMode = !createMode;
    $("auth-submit").textContent = createMode ? "CRIAR CONTA" : "ENTRAR";
    $("signup-toggle").textContent = createMode ? "VOLTAR AO LOGIN" : "CRIAR ACESSO";
    $("auth-hint").textContent = createMode
      ? "Crie uma conta com e-mail e senha; depois confirme o e-mail recebido. As regras do Firestore permitem somente a conta proprietária."
      : "Na primeira configuração, crie sua conta e confirme o e-mail. O acesso aos dados só libera para o e-mail proprietário definido nas regras do Firestore.";
  });
  $("auth-form").addEventListener("submit", async e => {
    e.preventDefault(); message("");
    const email = $("auth-email").value.trim();
    const password = $("auth-password").value;
    try {
      if (createMode) {
        const credential = await createUserWithEmailAndPassword(auth, email, password);
        await sendEmailVerification(credential.user);
        await signOut(auth);
        createMode = false;
        $("auth-submit").textContent = "ENTRAR";
        $("signup-toggle").textContent = "CRIAR ACESSO";
        message("Conta criada. Confirme o e-mail e depois entre. Copie o UID mostrado após entrar para configurar a regra de proprietário.", true);
      } else {
        const credential = await signInWithEmailAndPassword(auth, email, password);
        if (!credential.user.emailVerified) {
          await sendEmailVerification(credential.user);
          await signOut(auth);
          message("Confirme seu e-mail antes de acessar. Reenviamos o link de confirmação.");
        }
      }
    } catch (error) { message(explainError(error)); }
  });
  onAuthStateChanged(auth, async current => {
    user = current;
    $("auth-panel").hidden = !!current;
    $("app-panel").hidden = !current;
    if (!current) { privateKey = null; clearTimeout(keyLockTimer); $("key-passphrase").value = ""; return; }
    $("session-email").textContent = current.email || "Conta do vendedor";
    $("uid-line").hidden = false;
    $("uid-line").textContent = `UID proprietário: ${current.uid}`;
    $("signout").onclick = async () => { privateKey = null; clearTimeout(keyLockTimer); $("key-passphrase").value = ""; await signOut(auth); };
    try { await refreshClients(); await loadKeyState(); }
    catch (error) { message(explainError(error)); }
  });
}

async function bootstrapCounter() {
  const counterRef = doc(db, "system", "clientCounter");
  const list = await getDocs(query(collection(db, "clients"), orderBy("sequence", "desc"), limit(1)));
  const maxExisting = list.empty ? 0 : Number(list.docs[0].data().sequence || 0);
  await runTransaction(db, async tx => {
    const snap = await tx.get(counterRef);
    const current = snap.exists() ? Number(snap.data().lastIssued || 0) : 0;
    tx.set(counterRef, { lastIssued: Math.max(current, maxExisting), updatedAt: serverTimestamp() }, { merge: true });
  });
}
async function refreshClients() {
  const snapshot = await getDocs(query(collection(db, "clients"), orderBy("sequence", "asc")));
  clients = snapshot.docs.map(d => ({ ...d.data(), code: d.id }));
  await bootstrapCounter();
  const body = $("clients-body"); body.replaceChildren();
  if (!clients.length) {
    const row = body.insertRow(); const cell = row.insertCell(); cell.colSpan=4; cell.className="empty"; cell.textContent="Cadastre os clientes 001 e 002 existentes para iniciar.";
  }
  for (const client of clients) {
    const row = body.insertRow(); row.title = "Abrir cadastro";
    const code = row.insertCell(); code.textContent = client.code;
    const name = row.insertCell(); name.textContent = client.name || "";
    const plan = row.insertCell(); plan.textContent = (client.plan || "").toUpperCase();
    const state = row.insertCell(); state.textContent = client.active ? "ATIVA" : "INATIVA"; state.className = client.active ? "status-active" : "status-inactive";
    row.addEventListener("click", () => loadClient(client));
  }
  const counter = await getDoc(doc(db, "system", "clientCounter"));
  const next = (Number(counter.data()?.lastIssued || 0) + 1).toString().padStart(3, "0");
  $("client-code-badge").textContent = `PRÓXIMO CÓDIGO ${next}`;
}

function loadClient(c) {
  $("client-code").value = c.code;
  $("client-name").value = c.name || "";
  $("client-document").value = c.document || "";
  $("client-serial").value = c.serial || "";
  $("client-plan").value = c.plan || "standard";
  $("client-extra").value = Number(c.additionalTerminals || 0);
  $("client-billing").value = c.billingMode || "unico";
  $("client-expiry").value = c.expiresLocalDate || "";
  $("client-contact").value = c.sellerContact || "";
  $("client-active").checked = c.active !== false;
  $("form-title").textContent = `Cliente ${c.code}`;
  $("client-code-badge").textContent = `CÓDIGO ${c.code}`;
  $("issue-license").disabled = !privateKey;
  $("downloads").hidden = true;
}
function clearForm() {
  $("client-form").reset();
  $("client-code").value = "";
  $("client-extra").value = "0";
  $("client-active").checked = true;
  $("client-expiry").disabled = true;
  $("form-title").textContent = "Novo cliente";
  $("issue-license").disabled = true;
  $("client-code-badge").textContent = "PRÓXIMO CÓDIGO AUTOMÁTICO";
  $("downloads").hidden = true;
}
$("client-billing").addEventListener("change", () => {
  const monthly = $("client-billing").value === "mensal";
  $("client-expiry").disabled = !monthly;
  $("client-expiry").required = monthly;
});
$("new-client").addEventListener("click", clearForm);
$("clear-form").addEventListener("click", clearForm);
$("refresh").addEventListener("click", async () => { try { await refreshClients(); message("Lista atualizada.", true); } catch(e) { message(explainError(e)); } });

function readClientForm() {
  const code = $("client-code").value.trim();
  if (code && !/^[0-9]{3,9}$/.test(code)) throw new Error("Use um código numérico com pelo menos três dígitos, como 003.");
  const serial = $("client-serial").value.trim().toUpperCase();
  if (!/^LI-(?:[0-9A-F]{4}-){3}[0-9A-F]{4}$/.test(serial)) throw new Error("Confira o serial completo do PDV.");
  const billingMode = $("client-billing").value;
  const expiresLocalDate = billingMode === "mensal" ? $("client-expiry").value : "";
  if (billingMode === "mensal" && !expiresLocalDate) throw new Error("Escolha a data de validade da mensalidade.");
  return {
    code, name: $("client-name").value.trim(), document: $("client-document").value.trim(), serial,
    plan: $("client-plan").value, additionalTerminals: Number($("client-extra").value || 0),
    billingMode, expiresLocalDate, sellerContact: $("client-contact").value.trim(), active: $("client-active").checked
  };
}
async function saveClient(e) {
  e.preventDefault(); message("");
  try {
    const data = readClientForm();
    if (!data.name) throw new Error("Informe o nome do cliente.");
    if (!Number.isInteger(data.additionalTerminals) || data.additionalTerminals < 0 || data.additionalTerminals > 998) throw new Error("Pontos adicionais deve ficar entre 0 e 998.");
    if (data.expiresLocalDate && new Date(`${data.expiresLocalDate}T23:59:59`) <= new Date()) throw new Error("A data da mensalidade deve ser futura.");
    let code = data.code;
    if (code) {
      const sequence = Number(code);
      const clientRef = doc(db, "clients", code);
      const counterRef = doc(db, "system", "clientCounter");
      await runTransaction(db, async tx => {
        const [clientSnap, counterSnap] = await Promise.all([tx.get(clientRef), tx.get(counterRef)]);
        const counter = Number(counterSnap.data()?.lastIssued || 0);
        if (!clientSnap.exists()) {
          tx.set(clientRef, { ...data, sequence, lastRevision: 0, createdAt: serverTimestamp(), updatedAt: serverTimestamp() });
          tx.set(counterRef, { lastIssued: Math.max(counter, sequence), updatedAt: serverTimestamp() }, { merge: true });
        } else {
          tx.update(clientRef, { ...data, updatedAt: serverTimestamp() });
          tx.set(counterRef, { lastIssued: Math.max(counter, sequence), updatedAt: serverTimestamp() }, { merge: true });
        }
      });
    } else {
      const counterRef = doc(db, "system", "clientCounter");
      await runTransaction(db, async tx => {
        const counterSnap = await tx.get(counterRef);
        const sequence = Number(counterSnap.data()?.lastIssued || 0) + 1;
        code = sequence.toString().padStart(3, "0");
        const clientRef = doc(db, "clients", code);
        const clientSnap = await tx.get(clientRef);
        if (clientSnap.exists()) throw new Error(`O código ${code} já existe. Atualize a lista e tente novamente.`);
        tx.set(counterRef, { lastIssued: sequence, updatedAt: serverTimestamp() }, { merge: true });
        tx.set(clientRef, { ...data, code, sequence, lastRevision: 0, createdAt: serverTimestamp(), updatedAt: serverTimestamp() });
      });
    }
    await refreshClients();
    loadClient(clients.find(c => c.code === code));
    message(`Cadastro ${code} salvo na nuvem.`, true);
  } catch (error) { message(explainError(error)); }
}
$("client-form").addEventListener("submit", saveClient);

const sellerPublicKeyPem = `-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEA1dmfpWZYBdgSQWJJgV+8
jdYHXsjupUba1ceGY7jmJ+OTZ2UCZegj1jd7O2tAc/xB9YbcPl+t3pQg+F2Rsn5w
ljioMuzAHa5rIxHgP6zlfDd89IVk4JHUaJbJCVWiGPf+SQmzKPSaOtacK5SNIPSD
Y4G0FaGrAWBC4j4cDsIP7vpKR5VkBc8+hSVj1PSMci1ugtdsDk69wKKS5PJih7nO
UjX+DdpXx9ZJXHY9LrW8/PXnfOgGa4dESMBPXzTBJahCmRW/2DHsbqyoZH1VDIb3
+KhgawEiS5OqkTzsAqNstRitX8laBBOWnnrssQCnxSJai8A+n/x3IGLpCtRP+KVB
baPs6jo02HQCR7UgqTgcE1q4WTOKaOaJmNs03OyBopEYhM2xyLMCFL4XQ/cZM4vg
VX7phBg35EJgZ2/+vuHl2ypGu2iAXruXrAw362m8Pqnb5FMm6KcosUqkzQicqi0u
JdqQI5mayPP/iDpK79stIpnVbd5Pj/fvOTjO+kXI2cqVAgMBAAE=
-----END PUBLIC KEY-----`;
async function checkKey(privatePem) {
  const key = await crypto.subtle.importKey("pkcs8", privateKeyBytes(privatePem), { name: "RSA-PSS", hash: "SHA-256" }, false, ["sign"]);
  const publicKey = await crypto.subtle.importKey("spki", pemBytes(sellerPublicKeyPem, "PUBLIC KEY"), { name: "RSA-PSS", hash: "SHA-256" }, false, ["verify"]);
  const test = new TextEncoder().encode("LEAL-INFO-SELLER-KEY-CHECK");
  const sig = await crypto.subtle.sign({ name: "RSA-PSS", saltLength: 32 }, key, test);
  if (!await crypto.subtle.verify({ name: "RSA-PSS", saltLength: 32 }, publicKey, sig, test)) throw new Error("A chave privada não corresponde à chave pública do PDV.");
  return key;
}
function refreshKeyLock() {
  clearTimeout(keyLockTimer);
  if (!privateKey) return;
  keyLockTimer = setTimeout(() => {
    privateKey = null;
    $("issue-license").disabled = true;
    $("key-state").textContent = "Bloqueada por inatividade";
    $("key-state").classList.remove("ready");
    message("A chave foi bloqueada após 15 minutos sem atividade. Digite a frase secreta para desbloquear novamente.");
  }, 15 * 60 * 1000);
}
for (const eventName of ["pointerdown", "keydown"]) document.addEventListener(eventName, refreshKeyLock, { passive:true });
async function encryptPrivateKey(pem, passphrase) {
  const salt = crypto.getRandomValues(new Uint8Array(16));
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const material = await crypto.subtle.importKey("raw", new TextEncoder().encode(passphrase), "PBKDF2", false, ["deriveKey"]);
  const key = await crypto.subtle.deriveKey({ name: "PBKDF2", salt, iterations: 600000, hash: "SHA-256" }, material, { name: "AES-GCM", length: 256 }, false, ["encrypt"]);
  const cipher = await crypto.subtle.encrypt({ name: "AES-GCM", iv }, key, new TextEncoder().encode(pem));
  return { version: 1, cipher: bytesToBase64(new Uint8Array(cipher)), salt: bytesToBase64(salt), iv: bytesToBase64(iv), iterations: 600000, updatedAt: serverTimestamp() };
}
async function decryptPrivateKey(record, passphrase) {
  const material = await crypto.subtle.importKey("raw", new TextEncoder().encode(passphrase), "PBKDF2", false, ["deriveKey"]);
  const key = await crypto.subtle.deriveKey({ name: "PBKDF2", salt: base64ToBytes(record.salt), iterations: record.iterations, hash: "SHA-256" }, material, { name: "AES-GCM", length: 256 }, false, ["decrypt"]);
  const clear = await crypto.subtle.decrypt({ name: "AES-GCM", iv: base64ToBytes(record.iv) }, key, base64ToBytes(record.cipher));
  return new TextDecoder().decode(clear);
}
async function loadKeyState() {
  const snap = await getDoc(doc(db, "system", "signingKey"));
  const ready = snap.exists();
  $("key-state").textContent = ready ? "Protegida na nuvem" : "Não configurada";
  $("key-state").classList.toggle("ready", ready);
  $("unlock-key").hidden = !ready;
}
$("save-key").addEventListener("click", async () => {
  try {
    const file = $("private-key-file").files[0];
    const passphrase = $("key-passphrase").value;
    if (!file) throw new Error("Selecione a chave privada original do vendedor (.pem).");
    if (passphrase.length < 12) throw new Error("Use uma frase secreta com pelo menos 12 caracteres.");
    const pem = await file.text();
    privateKey = await checkKey(pem);
    await setDoc(doc(db, "system", "signingKey"), await encryptPrivateKey(pem, passphrase));
    $("key-state").textContent = "Protegida e desbloqueada"; $("key-state").classList.add("ready");
    $("unlock-key").hidden = false; $("issue-license").disabled = !$("client-code").value;
    $("key-passphrase").value = "";
    refreshKeyLock();
    message("Chave protegida e sincronizada. Guarde bem a frase secreta; ela será necessária nos outros computadores.", true);
  } catch (error) { privateKey = null; message(explainError(error)); }
});
$("unlock-key").addEventListener("click", async () => {
  try {
    const passphrase = $("key-passphrase").value;
    if (passphrase.length < 12) throw new Error("Digite a frase secreta usada para proteger a chave.");
    const snap = await getDoc(doc(db, "system", "signingKey"));
    if (!snap.exists()) throw new Error("A chave protegida ainda não foi configurada.");
    privateKey = await checkKey(await decryptPrivateKey(snap.data(), passphrase));
    $("key-passphrase").value = "";
    $("key-state").textContent = "Desbloqueada neste navegador"; $("key-state").classList.add("ready");
    $("issue-license").disabled = !$("client-code").value;
    refreshKeyLock();
    message("Chave desbloqueada neste navegador. Ela será esquecida ao sair da conta.", true);
  } catch (error) { privateKey = null; message(error?.name === "OperationError" ? "Frase secreta incorreta ou chave criptografada inválida." : explainError(error)); }
});

function utcExpiry(localDate) {
  if (!localDate) return null;
  const [year, month, day] = localDate.split("-").map(Number);
  const localEnd = new Date(year, month - 1, day, 23, 59, 59, 0);
  return localEnd.toISOString();
}
function toBase64Utf8(text) { return bytesToBase64(new TextEncoder().encode(text)); }
function downloadLink(container, fileName, content, mime, label) {
  const url = URL.createObjectURL(new Blob([content], { type: mime }));
  const a = document.createElement("a"); a.href=url; a.download=fileName; a.textContent=label;
  a.addEventListener("click", () => setTimeout(() => URL.revokeObjectURL(url), 30000), { once:true });
  container.append(a);
}
$("issue-license").addEventListener("click", async () => {
  try {
    if (!privateKey) throw new Error("Desbloqueie primeiro a chave do vendedor.");
    let data = readClientForm();
    if (!data.code) { await saveClient(new Event("submit", { cancelable:true })); data = readClientForm(); }
    if (!data.code || !clients.some(c => c.code === data.code)) throw new Error("Salve o cadastro do cliente antes de gerar a licença.");
    if (data.billingMode === "mensal" && !data.expiresLocalDate) throw new Error("Informe a validade da mensalidade.");
    if (data.expiresLocalDate && new Date(`${data.expiresLocalDate}T23:59:59`) <= new Date()) throw new Error("A validade da mensalidade deve ser futura.");
    const ref = doc(db, "clients", data.code);
    const revision = await runTransaction(db, async tx => {
      const snap = await tx.get(ref);
      if (!snap.exists()) throw new Error("O cadastro não está salvo na nuvem.");
      const last = Number(snap.data().lastRevision || 0);
      const next = Math.max(Date.now(), last + 1);
      tx.update(ref, { ...data, lastRevision: next, updatedAt: serverTimestamp() });
      return next;
    });
    const terms = {
      ServerSerial: data.serial,
      ComputerLimit: 2 + data.additionalTerminals,
      BillingMode: data.billingMode,
      ExpiresUtc: data.billingMode === "mensal" ? utcExpiry(data.expiresLocalDate) : null,
      SellerContact: data.sellerContact,
      Revision: revision,
      LicenseId: crypto.randomUUID(),
      Plan: data.plan,
      ClientCode: data.code,
      Active: data.active,
      AdditionalTerminals: data.additionalTerminals
    };
    const payload = new TextEncoder().encode(JSON.stringify(terms));
    const signature = await crypto.subtle.sign({ name:"RSA-PSS", saltLength:32 }, privateKey, payload);
    const envelope = { Payload: bytesToBase64(payload), Signature: bytesToBase64(new Uint8Array(signature)) };
    const licenseText = JSON.stringify(envelope);
    const historyRef = doc(db, "clients", data.code, "licenses", String(revision));
    await setDoc(historyRef, { revision, signedLicense: licenseText, terms, generatedAt: serverTimestamp() });
    await refreshClients(); loadClient(clients.find(c => c.code === data.code));
    const links = $("downloads"); links.replaceChildren(); links.hidden=false;
    const safeCode = data.code.replace(/[^0-9]/g, "");
    downloadLink(links, `Licenca_${safeCode}_${data.plan}.leallicenca`, licenseText, "application/json", "BAIXAR .LEALLICENCA");
    downloadLink(links, `Licenca_${safeCode}_${data.plan}_chave.txt`, `LEAL1-${toBase64Utf8(licenseText)}`, "text/plain;charset=utf-8", "BAIXAR CHAVE .TXT");
    message(`Licença do cliente ${data.code} criada e registrada. Baixe o arquivo ou a chave abaixo.`, true);
  } catch (error) { message(explainError(error)); }
});

$("client-code").addEventListener("input", () => { $("issue-license").disabled = !privateKey || !clients.some(c => c.code === $("client-code").value.trim()); });
