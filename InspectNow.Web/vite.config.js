import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
  const { INSPECTNOW_API_URL: target = 'https://localhost:7145' } = loadEnv(mode, process.cwd(), 'INSPECTNOW_');
  const local = ['localhost', '127.0.0.1', '[::1]'].includes(new URL(target).hostname);
  return {
    plugins: [react()],
    server: { host: '127.0.0.1', proxy: { '/api': { target, secure: !local } } },
  };
});
