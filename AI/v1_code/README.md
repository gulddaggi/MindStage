# How to Use JupyterLab Server on Local VS Code with Tunnel

1. Download VS Code CLI:
   ```bash
   wget -O vscode_cli.tar.gz "https://code.visualstudio.com/sha/download?build=stable&os=cli-alpine-x64"
   tar -xzf vscode_cli.tar.gz
   # The extracted directory will contain the 'code' executable
   ./code tunnel
   ```

2. Follow the login prompts to authenticate.

3. Once tunneling is active, a link will be displayed (tunneling as ...). Use this link to access your remote environment.

4. On your local VS Code, install the 'Remote - Tunnel' extension.

5. You can now use your remote JupyterLab server from your local VS Code.

---

## AIHub Commands Used

Example command to download dataset:

```bash
./aihubshell -mode d -datasetkey 263 -aihubapikey '<YOUR_AIHUB_API_KEY>'
```

- `-mode d` : Download mode
- `-datasetkey 263` : Dataset key
- `-aihubapikey <YOUR_AIHUB_API_KEY>` : Your AIHub API key (replace with your own)
