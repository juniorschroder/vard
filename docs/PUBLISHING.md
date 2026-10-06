# Guia Operacional de Publicação NuGet (Vard)

Este documento estabelece o processo padrão e seguro para empacotar, validar e publicar versões oficiais do pacote **Vard** no repositório público [NuGet.org](https://www.nuget.org).

---

## 1. Pré-Requisitos

Antes de iniciar o procedimento de release:

1. **Conta no NuGet.org:** Possuir uma conta ativa e autenticada no [nuget.org](https://www.nuget.org).
2. **API Key com Escopo Restrito:**
   - Acesse [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys).
   - Clique em **Create**.
   - Defina um nome identificável (ex: `Vard Release Key`).
   - Configure a expiração desejada (ex: 90 ou 365 dias).
   - Em **Package Permissions**, selecione **Push new packages and package versions**.
   - Em **Glob Pattern**, restrinja o escopo especificando `Vard*` ou `Vard`.
   - Copie a chave gerada imediatamente e armazene-a de forma segura em seu gerenciador de credenciais.
3. **Ambiente Local:**
   - .NET SDK 8.0 ou superior instalado (`dotnet --version`).
   - Repositório Git sincronizado com a branch `master` limpa e atualizada.

---

## 2. Checklist Pré-Release

Execute as seguintes etapas sequenciais para garantir que a release atenda aos padrões estritos de qualidade:

### 2.1. Verificação da Versão e Metadados
- Confirme que a versão em `src/Vard/Vard.csproj` corresponde à versão pretendida (ex: `<Version>1.0.0</Version>`).
- Confirme que o `CHANGELOG.md` contém a seção correspondente com todas as adições, correções ou mudanças listadas.

### 2.2. Compilação Estrita Release
Garanta que não existam avisos (warnings) de compilação ou de documentação XML:

```bash
dotnet build src/Vard/Vard.csproj -c Release /p:TreatWarningsAsErrors=true
```

### 2.3. Execução da Suíte Completa de Testes
Todos os testes automatizados devem ser executados e aprovados:

```bash
dotnet test tests/Vard.Tests/Vard.Tests.csproj -c Release
```

---

## 3. Empacotamento NuGet

Gere os pacotes `.nupkg` (binários, documentação XML e README) e `.snupkg` (símbolos para depuração / SourceLink) no diretório isolado `./artifacts`:

```bash
dotnet pack src/Vard/Vard.csproj -c Release -o ./artifacts
```

> **Aviso de Segurança (T-06-05):** O diretório `artifacts/` e extensões `*.nupkg`/`*.snupkg` estão configurados no `.gitignore` para prevenir inclusão acidental de binários no controle de versão Git.

### 3.1. Inspeção de Integridade do Pacote

Valide que o pacote foi gerado com êxito e que não contém dependências externas transitivas:

```bash
# 1. Verificar presença dos artefatos
ls -lh ./artifacts/Vard.*.nupkg ./artifacts/Vard.*.snupkg

# 2. Inspecionar o manifesto .nuspec para comprovar zero dependências em runtime
unzip -p ./artifacts/Vard.1.0.0.nupkg Vard.nuspec | grep -A 2 "<dependencies>"
```

O bloco de dependências para `.NETStandard2.1` deve ser estritamente vazio:
```xml
<dependencies>
  <group targetFramework=".NETStandard2.1" />
</dependencies>
```

```bash
# 3. Inspecionar os arquivos embutidos no pacote
unzip -l ./artifacts/Vard.1.0.0.nupkg
```

Confirme a presença de:
- `lib/netstandard2.1/Vard.dll`
- `lib/netstandard2.1/Vard.xml`
- `README.md`

---

## 4. Publicação no NuGet.org

> **Importante (Prevenção de Vazamento de Credenciais):** Nunca passe a API Key diretamente como texto legível no histórico do terminal ou em scripts persistidos no repositório. Utilize uma variável de ambiente temporária ou mascarada.

### 4.1. Definindo a API Key no Ambiente

No Linux / macOS:
```bash
read -s -p "Informe a NuGet API Key: " NUGET_API_KEY
echo ""
export NUGET_API_KEY
```

No Windows (PowerShell):
```powershell
$NUGET_API_KEY = Read-Host -AsSecureString "Informe a NuGet API Key"
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($NUGET_API_KEY)
$env:NUGET_API_KEY = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
```

### 4.2. Executando o Push

Envie o pacote binário para o NuGet.org:

```bash
dotnet nuget push ./artifacts/Vard.1.0.0.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

*Nota:* O comando `dotnet nuget push` detecta automaticamente o pacote de símbolos companheiro (`Vard.1.0.0.snupkg`) no mesmo diretório e o publica para o servidor de símbolos oficial do NuGet (`https://symbols.nuget.org/download/symbols`), viabilizando depuração passo a passo com SourceLink pelos consumidores da biblioteca.

### 4.3. Limpeza das Credenciais da Sessão

Após a conclusão do envio, limpe a variável da memória da sessão:

```bash
unset NUGET_API_KEY
```

---

## 5. Verificação Pós-Publicação

1. **Indexação no Portal:**
   - Acesse `https://www.nuget.org/packages/Vard/1.0.0`.
   - Aguarde o processamento de indexação e validação de malware (geralmente entre 5 e 15 minutos).
   - Verifique se a descrição, badges, README e licença MIT são renderizados corretamente na página do pacote.

2. **Teste de Consumo Real (Smoke Test):**
   - Crie uma aplicação de teste isolada para validar a instalação a partir do índice público:
     ```bash
     mkdir /tmp/VardSmokeTest && cd /tmp/VardSmokeTest
     dotnet new console
     dotnet add package Vard --version 1.0.0
     dotnet run
     ```
   - Confirme que a aplicação restaura o pacote sem conflitos e compila normalmente.

---

## 6. Criação de Release Tag no Git

Após a confirmação da publicação no nuget.org, registre a tag de release correspondente no Git:

```bash
git tag -a v1.0.0 -m "Release v1.0.0 - Vard Resilience Toolkit"
git push origin v1.0.0
```
