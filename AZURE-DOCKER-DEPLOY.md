# Deploy das imagens Docker no Azure App Service

O Terraform cria o **Azure Container Registry (ACR)** e os **Web Apps** (API e UI) configurados para usar imagens Docker desse registro. Após `terraform apply`, é preciso fazer **build** e **push** das imagens para o ACR.

## 1. Após o Terraform apply

Anote os outputs:

- `acr_name` – nome do ACR (ex.: `agripewebacrxxxx`)
- `acr_login_server` – URL do registro (ex.: `agripewebacrxxxx.azurecr.io`)
- `api_url` / `ui_url` – URLs dos Web Apps

## 2. Login no ACR

Dentro da pasta de Terraform do Azure (`terraform/azure`, onde está o `main.tf` do Azure):

```powershell
cd terraform/azure
az acr login --name (terraform output -raw acr_name)
```

Ou use o nome direto: `az acr login --name agripewebacr19cba48c`

## 3. Build e push da API

A partir da raiz do repositório (para que os caminhos dos Dockerfiles funcionem), use o Terraform que está em `terraform/azure` com `-chdir`:

```powershell
cd D:\Source\Repos\cclautert\AgripeWeb
$acr = terraform -chdir=terraform/azure output -raw acr_login_server
docker build -t "${acr}/agripeweb-api:latest" -f AgripeWebAPI/Dockerfile AgripeWebAPI
docker push "${acr}/agripeweb-api:latest"
```

## 4. Build e push da UI

```powershell
$acr = terraform -chdir=terraform/azure output -raw acr_login_server
docker build -t "${acr}/agripeweb-ui:latest" -f AgripeWebUI/Dockerfile AgripeWebUI
docker push "${acr}/agripeweb-ui:latest"
```

Se `$acr` ficar vazio, defina o login server do ACR manualmente:

```powershell
$acr = "agripewebacr19cba48c.azurecr.io"   # substitua pelo seu acr_login_server
docker build -t "${acr}/agripeweb-api:latest" -f AgripeWebAPI/Dockerfile AgripeWebAPI
docker push "${acr}/agripeweb-api:latest"
docker build -t "${acr}/agripeweb-ui:latest" -f AgripeWebUI/Dockerfile AgripeWebUI
docker push "${acr}/agripeweb-ui:latest"
```

## 5. Iniciar os Web Apps (Start)

Se os sites estiverem **parados** (403 Site Disabled, ou Overview mostra "Stopped"), inicie os dois:

```powershell
az webapp start --name agripeweb-api --resource-group rg-agripeweb
az webapp start --name agripeweb-ui --resource-group rg-agripeweb
```

**Conferir estado:**

```powershell
az webapp show --name agripeweb-api --resource-group rg-agripeweb --query state -o tsv
az webapp show --name agripeweb-ui --resource-group rg-agripeweb --query state -o tsv
```

Deve retornar `Running`. Se o `start` não mudar nada ou der erro:

- No **Portal Azure**: **App Service** → **agripeweb-api** (ou **agripeweb-ui**) → **Overview** → botão **Start**.
- Confira se a **assinatura** está ativa (sem aviso de crédito/suspensão).

**Plano F1 (Free):** no tier **F1**, o Azure pode **parar** os apps após cerca de 20 minutos sem acesso. Não é defeito: é limitação do plano gratuito. Ao acessar de novo, o site pode demorar um pouco para “acordar”. Para manter os dois apps **sempre ligados**, é preciso usar um plano pago (ex.: **B1**): no `main.tf` altere `sku_name` de `"F1"` para `"B1"` no `azurerm_service_plan` e rode `terraform apply`.

**Estado QuotaExceeded:** se `az webapp show ... --query state` retornar **QuotaExceeded**, o plano ou a assinatura atingiu a cota (ex.: limite de apps no F1). Os sites não sobem e o `start` não resolve. Opções: **(1)** No Portal, **Assinaturas** → sua assinatura → **Uso + cotas** → procurar "App Service" ou "Free VMs" e solicitar aumento de cota; **(2)** Migrar para o plano **B1**: no `main.tf` altere `sku_name` para `"B1"` no `azurerm_service_plan` e rode `terraform apply` (a cota B1 costuma permitir os dois Web Apps).

## 6. Reiniciar os Web Apps (opcional)

Após o push, o App Service pode puxar a nova imagem sozinho; se não atualizar, reinicie pelo Portal ou CLI:

```powershell
az webapp restart --name agripeweb-api --resource-group rg-agripeweb
az webapp restart --name agripeweb-ui --resource-group rg-agripeweb
```

## 503 Service Unavailable (com state Running)

Se `az webapp show ... --query state` retornar **Running** mas o site responder **503** no navegador (com **Retry-After: 60**), o container não está atendendo na porta 80 — em geral porque **o container quebra ao iniciar** (ex.: nginx com upstream "agripewebapi" que não existe no Azure) ou a **imagem em uso ainda é a antiga**.

**Passos:**

1. **Reconstruir e enviar a imagem da UI** (com entrypoint que usa a URL da API no Azure por padrão):
   ```powershell
   $acr = terraform output -raw acr_login_server
   docker build -t "${acr}/agripeweb-ui:latest" -f AgripeWebUI/Dockerfile AgripeWebUI
   docker push "${acr}/agripeweb-ui:latest"
   ```
2. **Reiniciar o Web App** e aguardar 2–3 minutos (o pull da nova imagem e o startup do container levam um tempo):
   ```powershell
   az webapp restart --name agripeweb-ui --resource-group rg-agripeweb
   ```
3. No **Portal** → **agripeweb-ui** → **Deployment Center**: confira se a imagem é a do ACR com tag `latest` e, se houver, use **Sync** ou **Redeploy** para forçar o uso da imagem mais recente.
4. (Opcional) **Habilitar logs do container em arquivo**, para inspecionar se o log stream der 504: **Monitoring** → **App Service logs** → ativar **Application Logging (Filesystem)** e **Docker Container logging**; depois **Advanced Tools (Kudu)** → **Zip Log Files** ou **Download support package** para ver os logs.

## Erro "Application Error" na UI

Se ao acessar `https://agripeweb-ui.azurewebsites.net` aparecer **Application Error**:

1. **Confirme que a imagem foi enviada ao ACR**  
   No Portal: **Container Registry** → seu ACR → **Repositories** → deve existir `agripeweb-ui` com tag `latest`.

2. **Veja os logs do container**  
   No Portal: **App Service** → **agripeweb-ui** → **Monitoring** → **Log stream**. Ou pelo CLI:
   ```powershell
   az webapp log tail --name agripeweb-ui --resource-group rg-agripeweb
   ```
   Verifique se o container sobe (nginx) ou se há falha ao puxar a imagem / na porta.

3. **Reinicie o Web App** (para forçar novo pull da imagem):
   ```powershell
   az webapp restart --name agripeweb-ui --resource-group rg-agripeweb
   ```

4. **Configuração do container no Portal**  
   **App Service** → **agripeweb-ui** → **Deployment Center** → confira se a imagem está `agripewebacrxxxx.azurecr.io/agripeweb-ui:latest` e que o **Registry** está correto (Managed Identity).

5. **API em outro App Service**  
   A UI usa a variável de ambiente **`API_BASE_URL`** (definida no Terraform para `https://agripeweb-api.azurewebsites.net`) para o nginx fazer proxy de `/api` para a API. Depois de alterar essa config ou o Terraform, faça novo build e push da imagem da UI e reinicie o Web App.

## Variáveis de ambiente da API no Azure

A API precisa de **MongoDB** e, se usar, **JWT/OAuth**. Configure em **App Service → Configuration → Application settings** (ou via Terraform `app_settings`) as variáveis necessárias, por exemplo:

- `MongoDb__ConnectionString`
- `MongoDb__DatabaseName`
- `ASPNETCORE_ENVIRONMENT=Production`
