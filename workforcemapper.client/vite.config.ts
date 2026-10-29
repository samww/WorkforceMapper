import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vitest/config';
import type { ProxyOptions } from 'vite';
import plugin from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';

const isDev = env.NODE_ENV !== 'production';

let serverConfig = {};

if (isDev) {
    const baseFolder =
        env.APPDATA !== undefined && env.APPDATA !== ''
            ? `${env.APPDATA}/ASP.NET/https`
            : `${env.HOME}/.aspnet/https`;

    const certificateName = "workforcemapper.client";
    const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
    const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

    if (!fs.existsSync(baseFolder)) {
        fs.mkdirSync(baseFolder, { recursive: true });
    }

    if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
        if (0 !== child_process.spawnSync('dotnet', [
            'dev-certs',
            'https',
            '--export-path',
            certFilePath,
            '--format',
            'Pem',
            '--no-password',
        ], { stdio: 'inherit', }).status) {
            throw new Error("Could not create certificate.");
        }
    }

    const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
        env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:7023';

    const apiProxy: ProxyOptions = {
        target,
        secure: false,
        configure: (proxy) => {
            setTimeout(() => {
                proxy.removeAllListeners('error');
                proxy.on('error', (err) => {
                    const code = (err as NodeJS.ErrnoException).code;
                    if (code !== 'ECONNREFUSED' && code !== 'ECONNRESET') {
                        console.error('[vite] proxy error:', err);
                    }
                });
            }, 0);
        }
    };

    serverConfig = {
        proxy: {
            '^/api': apiProxy
        },
        port: parseInt(env.DEV_SERVER_PORT || '49549'),
        https: {
            key: fs.readFileSync(keyFilePath),
            cert: fs.readFileSync(certFilePath),
        }
    };
}

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin(), tailwindcss()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    build: {
        outDir: '../WorkforceMapper.Server/wwwroot',
        emptyOutDir: true,
    },
    test: {
        environment: 'jsdom',
        setupFiles: ['./vitest.setup.ts'],
        globals: true,
    },
    server: serverConfig,
})
