// Copia los documentos de Docs/ a Frontend/src/content/ para que la página "Ayuda y Capacitación" los muestre.
// Se ejecuta solo antes de "npm run dev" y "npm run build" (scripts predev y prebuild).
import { copyFileSync, existsSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const aqui = dirname(fileURLToPath(import.meta.url));
const docs = resolve(aqui, '../../Docs');
const destino = resolve(aqui, '../src/content');

const archivos = [
    ['Manual_Usuario_CILP.md', 'manual.md'],
    ['Cronograma_Capacitacion.md', 'cronograma.md'],
];

mkdirSync(destino, { recursive: true });
for (const [origen, nombre] of archivos) {
    const ruta = resolve(docs, origen);
    if (!existsSync(ruta)) {
        console.error(`Falta Docs/${origen}`);
        process.exit(1);
    }
    copyFileSync(ruta, resolve(destino, nombre));
}
console.log('Documentos de Ayuda actualizados en src/content');