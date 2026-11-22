import React, { useMemo, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { BookOpen, CalendarDays } from 'lucide-react';
import manualMd from '../../content/manual.md?raw';
import cronogramaMd from '../../content/cronograma.md?raw';

// Ayuda y Capacitación: muestra el manual de usuario y el cronograma de capacitación.
// Los textos son los archivos de src/content (el manual es copia de Docs/Manual_Usuario_CILP.md
// y el cronograma de Docs/Cronograma_Capacitacion.md: mantenerlos iguales).

// Misma regla de anclas que GitHub: así el índice del manual funciona en la aplicación y en el repositorio.
const slug = (texto) => texto.toLowerCase().trim().replace(/[^\p{L}\p{N}\s-]/gu, '').replace(/\s+/g, '-');

const textoDe = (children) => React.Children.toArray(children)
    .map(c => (typeof c === 'string' ? c : c?.props?.children ? textoDe(c.props.children) : ''))
    .join('');

// El manual trae su propio "Índice" (para quien lo lee en el repositorio): acá se reemplaza por el panel lateral.
const sinIndice = (md) => md.replace(/^## Índice\n[\s\S]*?\n---\n/m, '');

const extraerIndice = (md) => {
    const items = [];
    let enCodigo = false;
    for (const linea of md.split('\n')) {
        if (linea.startsWith('```')) { enCodigo = !enCodigo; continue; }
        if (enCodigo) continue;
        const m = /^(#{2,3})\s+(.+?)\s*$/.exec(linea);
        if (m) {
            const texto = m[2].replace(/[`*_]/g, '');
            items.push({ nivel: m[1].length, texto, id: slug(texto) });
        }
    }
    return items;
};

const irA = (id) => document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });

const encabezado = (Tag, clases) => ({ children }) => (
    <Tag id={slug(textoDe(children))} className={`scroll-mt-4 ${clases}`}>{children}</Tag>
);

const componentes = {
    h1: ({ children }) => <h1 className="text-2xl font-bold text-slate-800 mb-4">{children}</h1>,
    h2: encabezado('h2', 'text-xl font-bold text-slate-800 mt-10 mb-3 pb-2 border-b border-slate-200'),
    h3: encabezado('h3', 'text-base font-bold text-slate-700 mt-6 mb-2'),
    p: ({ children }) => <p className="text-sm text-slate-700 leading-relaxed my-3">{children}</p>,
    ul: ({ children }) => <ul className="list-disc pl-6 my-3 space-y-1.5 text-sm text-slate-700">{children}</ul>,
    ol: ({ children }) => <ol className="list-decimal pl-6 my-3 space-y-1.5 text-sm text-slate-700">{children}</ol>,
    li: ({ children }) => <li className="leading-relaxed">{children}</li>,
    strong: ({ children }) => <strong className="font-semibold text-slate-900">{children}</strong>,
    blockquote: ({ children }) => (
        <blockquote className="my-3 border-l-4 border-blue-300 bg-blue-50/60 rounded-r-xl px-4 py-1 text-slate-700">{children}</blockquote>
    ),
    hr: () => <hr className="my-6 border-slate-200" />,
    code: ({ children }) => <code className="px-1.5 py-0.5 rounded bg-slate-100 text-[12px] font-mono text-slate-800">{children}</code>,
    a: ({ href, children }) => (
        href?.startsWith('#')
            ? <a href={href} onClick={(e) => { e.preventDefault(); irA(href.slice(1)); }} className="text-blue-600 hover:underline">{children}</a>
            : <a href={href} target="_blank" rel="noreferrer" className="text-blue-600 hover:underline">{children}</a>
    ),
    table: ({ children }) => (
        <div className="my-4 overflow-x-auto rounded-xl border border-slate-200">
            <table className="w-full text-sm text-left">{children}</table>
        </div>
    ),
    thead: ({ children }) => <thead className="bg-slate-50 text-slate-600">{children}</thead>,
    th: ({ children, style }) => <th style={style} className="px-3 py-2 font-bold border-b border-slate-200 whitespace-nowrap">{children}</th>,
    td: ({ children, style }) => <td style={style} className="px-3 py-2 border-b border-slate-100 align-top text-slate-700">{children}</td>,
};

const PESTANAS = [
    { id: 'manual', label: 'Manual de usuario', icono: <BookOpen size={16} />, md: sinIndice(manualMd) },
    { id: 'cronograma', label: 'Cronograma de capacitación', icono: <CalendarDays size={16} />, md: cronogramaMd },
];

const AyudaPage = () => {
    const [activa, setActiva] = useState('manual');
    const pestana = PESTANAS.find(p => p.id === activa);
    const indice = useMemo(() => extraerIndice(pestana.md), [pestana]);

    return (
        <div className="space-y-6">
            <div>
                <h2 className="text-2xl font-bold text-slate-800">Ayuda y Capacitación</h2>
                <p className="text-sm text-slate-500">Manual de usuario y plan de capacitación del sistema.</p>
            </div>

            <div className="flex gap-2 border-b border-slate-200">
                {PESTANAS.map(p => (
                    <button
                        key={p.id}
                        onClick={() => setActiva(p.id)}
                        className={`flex items-center gap-2 px-4 py-2.5 text-sm font-semibold border-b-2 -mb-px transition-colors ${activa === p.id
                            ? 'border-blue-600 text-blue-600'
                            : 'border-transparent text-slate-500 hover:text-slate-700'}`}
                    >
                        {p.icono} {p.label}
                    </button>
                ))}
            </div>

            <div className="flex gap-8 items-start">
                {/* Índice */}
                <aside className="hidden lg:block w-64 shrink-0 sticky top-0 max-h-[calc(100vh-14rem)] overflow-y-auto bg-white border border-slate-200 rounded-2xl p-4">
                    <p className="text-[11px] font-bold text-slate-400 uppercase tracking-[0.15em] mb-3">Índice</p>
                    <nav className="space-y-0.5">
                        {indice.map(item => (
                            <button
                                key={item.id}
                                onClick={() => irA(item.id)}
                                className={`block w-full text-left rounded-lg px-2 py-1.5 transition-colors hover:bg-blue-50 hover:text-blue-700 ${item.nivel === 2
                                    ? 'text-[13px] font-semibold text-slate-700 mt-1.5'
                                    : 'text-xs text-slate-500 pl-5'}`}
                            >
                                {item.texto}
                            </button>
                        ))}
                    </nav>
                </aside>

                {/* Contenido */}
                <article className="flex-1 min-w-0 max-w-4xl bg-white border border-slate-200 rounded-2xl p-8 shadow-sm">
                    <ReactMarkdown remarkPlugins={[remarkGfm]} components={componentes}>{pestana.md}</ReactMarkdown>
                </article>
            </div>
        </div>
    );
};

export default AyudaPage;
