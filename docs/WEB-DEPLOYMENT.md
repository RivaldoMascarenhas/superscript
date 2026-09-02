# Guia de Implantação e Execução Web — UNIFAP Lab Manager

Este documento orienta a equipe de TI da UniFAP sobre como disponibilizar o executável para rodar com comando único no PowerShell:

```powershell
irm https://<sua-url>/lab | iex
```

---

## 1. Como Funciona a Mágica do `irm ... | iex`?

* **`irm`** (`Invoke-RestMethod`): Faz o download do script em texto puro a partir de uma URL web.
* **`| iex`** (`Invoke-Expression`): Executa o script baixado diretamente na memória do PowerShell, sem o usuário precisar baixar arquivos manualmente ou descompactar pastas.
* O script baixado (`lab.ps1`) é o **Bootstrapper**:
  1. Eleva privilégios para Administrador automaticamente via UAC.
  2. Valida se o .NET 8 Desktop Runtime está instalado (instala silenciosamente caso falte).
  3. Baixa o pacote oficial `UniFAP-LabManager.zip` (apenas ~4.6 MB).
  4. Extrai em `C:\ProgramData\UniFAP\LabManager\App\`.
  5. Cria atalho na Área de Trabalho e inicializa o Lab Manager imediatamente.

---

## 2. Três Maneiras de Hospedar e Usar na UniFAP

### Opção A — Servidor Web da Intranet UniFAP (Mais Rápido e Seguro)

Recomendado para uso dentro do campus, pois o tráfego fica 100% na rede local sem gastar internet externa.

1. No servidor web da intranet (ex: IIS, Apache ou Nginx em `intranet.unifapce.edu.br`):
   * Crie uma pasta acessível via HTTP/HTTPS, por exemplo: `http://intranet.unifapce.edu.br/lab/`
   * Coloque os dois arquivos gerados pelo comando `pwsh -File .\Publish.ps1`:
     * `lab.ps1` (renomeado para `index.html` ou `lab.ps1`)
     * `UniFAP-LabManager.zip`
2. Configure o servidor web para servir `.ps1` como texto (`text/plain`).
3. Em qualquer máquina da faculdade, basta abrir o PowerShell e rodar:
   ```powershell
   irm http://intranet.unifapce.edu.br/lab.ps1 | iex
   ```

---

### Opção B — GitHub Releases (Igual ao Chris Titus WinUtil)

Se o repositório estiver no GitHub:

1. Faça o commit do script [`lab.ps1`](file:///c:/Users/Rivaldo/OneDrive/Desktop/SUPERSCRIPT/lab.ps1) na branch `main`.
2. Vá em **Releases** no GitHub e crie uma nova Release (ex: `v1.0.0`).
3. Anexe o arquivo `dist/UniFAP-LabManager.zip` na release.
4. Qualquer técnico poderá rodar:
   ```powershell
   irm https://raw.githubusercontent.com/<SUA-ORGANIZACAO>/<SEU-REPOSITORIO>/main/lab.ps1 | iex
   ```

---

### Opção C — Redirecionamento com URL Curta (Ex: `unifap.edu.br/lab`)

Para ficar exatamente igual a `christitus.com/win`:

1. No gerenciador de DNS da UniFAP (ou Cloudflare / Nginx / IIS), crie uma regra de redirecionamento (Redirect HTTP 301 ou 302):
   * **URL de Origem**: `https://ti.unifap.edu.br/lab` ou `https://unifapce.edu.br/lab`
   * **URL de Destino**: O link direto do `lab.ps1` (no GitHub Raw ou servidor interno).
2. O técnico roda:
   ```powershell
   irm https://ti.unifap.edu.br/lab | iex
   ```

---

## 3. Teste em Bancada Local

Para testar o bootstrapper localmente em sua máquina agora mesmo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\lab.ps1
```
