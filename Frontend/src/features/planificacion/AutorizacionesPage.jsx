import React, { useState, useEffect } from 'react';
import {
    Plus, Search, FileText, Filter, Calendar, ChevronDown,
    CheckCircle2, Clock, Eye, Pencil, Trash2, Ban
} from 'lucide-react';
import agent, { getErrorMessage } from '../../api/agent';
import MensajeError from '../../common/MensajeError';
import { useAuth } from '../../auth/AuthContext';
import { PUNTOS } from '../../auth/puntosMenu';
import {
    TIPO_ENTREGA, TIPO_DESCARGA, estadoDe, esEditable,
    OrdenFormModal, DetalleOrdenModal, CancelarOrdenModal
} from './OrdenModales';

// ─────────── Página principal ───────────
const AutorizacionesPage = () => {
    const [ordenes, setOrdenes] = useState([]);
    const [loading, setLoading] = useState(true);
    // Cada pestaña tiene su punto de menú (según la especificación: Recepción opera descargas; Despacho, entregas).
    const { tienePunto } = useAuth();
    const verDescargas = tienePunto(PUNTOS.AUTORIZACIONES_DESCARGA);
    const verEntregas = tienePunto(PUNTOS.ORDENES_ENTREGA);
    const [tab, setTab] = useState(verDescargas ? 'descarga' : 'despacho');
    const [search, setSearch] = useState('');
    const [modalOpen, setModalOpen] = useState(false);
    const [detalleOrden, setDetalleOrden] = useState(null);
    const [editarOrden, setEditarOrden] = useState(null);
    const [cancelarOrden, setCancelarOrden] = useState(null);
    const [error, setError] = useState('');   // error al eliminar (ej. 409 si tiene pesadas)

    const load = () => {
        setLoading(true);
        agent.OrdenesTransporte.list()
            .then(data => { setOrdenes(data); setLoading(false); })
            .catch(() => setLoading(false));
    };

    useEffect(() => { load(); }, []);

    const handleDelete = async (item) => {
        if (window.confirm(`¿Está seguro de eliminar permanentemente la orden OT-${item.id_OrdenTransporte}? Esta acción eliminará los detalles y planificaciones asociadas.`)) {
            setError('');
            try {
                await agent.OrdenesTransporte.delete(item.id_OrdenTransporte);
                load();
            } catch (err) {
                console.error(err);
                setError(getErrorMessage(err, 'Error al eliminar la orden.'));
            }
        }
    };

    const filtered = ordenes.filter(item => {
        const tipo = item.ordenDetalle?.orden?.id_TipoOrden;
        const matchTab = tab === 'descarga' ? tipo === TIPO_DESCARGA : tipo === TIPO_ENTREGA;
        const q = search.toLowerCase();
        const matchSearch = !q ||
            (item.ordenDetalle?.producto?.descripcion ?? '').toLowerCase().includes(q) ||
            (item.razonSocialTransportista?.descripcion ?? '').toLowerCase().includes(q) ||
            String(item.id_OrdenTransporte).includes(q);
        return matchTab && matchSearch;
    });

    // Estado real de la orden (t_EstadoOrden / t_EstadoOrdenTransporte).
    const getEstadoBadge = (item) => {
        const { texto, tono } = estadoDe(item);
        if (tono === 'rojo') {
            return (
                <div className="flex items-center gap-1.5" title={`Motivo: ${item.ordenDetalle?.orden?.motivoAnulacion || 'Sin motivo'}`}>
                    <Ban size={14} className="text-red-500" />
                    <span className="text-xs font-bold text-red-600">{texto}</span>
                </div>
            );
        }
        return (
            <div className="flex items-center gap-1.5">
                <CheckCircle2 size={14} className={tono === 'verde' ? 'text-emerald-500' : 'text-blue-500'} />
                <span className={`text-xs font-bold ${tono === 'verde' ? 'text-emerald-600' : 'text-slate-600'}`}>{texto}</span>
            </div>
        );
    };

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                <div>
                    <h2 className="text-2xl font-bold text-slate-800">Planificación Logística</h2>
                    <p className="text-slate-500 text-sm">Autorizaciones de Descarga (CU060-CU065) · Órdenes de Entrega (CU066-CU071)</p>
                </div>
                <button
                    onClick={() => setModalOpen(true)}
                    className="bg-blue-600 text-white px-6 py-3 rounded-2xl font-bold flex items-center gap-2 hover:bg-blue-700 shadow-xl shadow-blue-500/20 transition-all active:scale-95"
                >
                    <Plus size={20} />
                    Nueva {tab === 'descarga' ? 'Autorización' : 'Entrega'}
                </button>
            </div>

            <MensajeError mensaje={error} onCerrar={() => setError('')} />

            {/* Tabs */}
            <div className="flex bg-white p-1 rounded-2xl shadow-sm border border-slate-100 max-w-md">
                {verDescargas && (
                    <button
                        onClick={() => setTab('descarga')}
                        className={`flex-1 py-2.5 rounded-xl font-bold text-sm transition-all ${tab === 'descarga' ? 'bg-slate-900 text-white shadow-lg' : 'text-slate-500 hover:bg-slate-50'}`}
                    >
                        Autorizaciones (Crudo)
                    </button>
                )}
                {verEntregas && (
                    <button
                        onClick={() => setTab('despacho')}
                        className={`flex-1 py-2.5 rounded-xl font-bold text-sm transition-all ${tab === 'despacho' ? 'bg-slate-900 text-white shadow-lg' : 'text-slate-500 hover:bg-slate-50'}`}
                    >
                        Entregas (Refinados)
                    </button>
                )}
            </div>

            {/* Table card */}
            <div className="bg-white rounded-3xl shadow-sm border border-slate-100 overflow-hidden">
                <div className="p-6 border-b border-slate-50 flex flex-col md:flex-row gap-4 justify-between">
                    <div className="relative flex-1 max-w-md">
                        <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={18} />
                        <input
                            type="text"
                            placeholder="Buscar por cliente, producto o ID..."
                            value={search}
                            onChange={e => setSearch(e.target.value)}
                            className="w-full bg-slate-50 border-none rounded-xl py-2.5 pl-10 pr-4 text-sm font-medium focus:ring-2 focus:ring-blue-500/20 outline-none"
                        />
                    </div>
                    <div className="flex gap-2">
                        <button className="p-2.5 rounded-xl border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors">
                            <Filter size={20} />
                        </button>
                        <button className="p-2.5 rounded-xl border border-slate-200 text-slate-600 hover:bg-slate-50 transition-colors flex items-center gap-2 text-sm font-bold">
                            <Calendar size={18} /> Período <ChevronDown size={14} />
                        </button>
                    </div>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full text-left">
                        <thead>
                            <tr className="text-slate-400 text-[11px] uppercase tracking-widest bg-slate-50/50">
                                <th className="px-8 py-4 font-bold">ID / Orden</th>
                                <th className="px-8 py-4 font-bold">Producto</th>
                                <th className="px-8 py-4 font-bold">Transportista</th>
                                <th className="px-8 py-4 font-bold">Vehículo</th>
                                <th className="px-8 py-4 font-bold">Cantidad</th>
                                <th className="px-8 py-4 font-bold">Estado</th>
                                <th className="px-8 py-4 font-bold">Fecha</th>
                                <th className="px-8 py-4 font-bold">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-50">
                            {loading ? (
                                [1, 2, 3].map(i => (
                                    <tr key={i} className="animate-pulse">
                                        <td colSpan="8" className="px-8 py-6 h-16 bg-slate-50/20" />
                                    </tr>
                                ))
                            ) : filtered.length === 0 ? (
                                <tr>
                                    <td colSpan="8" className="px-8 py-20 text-center text-slate-400 font-medium">
                                        <div className="flex flex-col items-center gap-3">
                                            <FileText size={48} className="opacity-10" />
                                            <p>No se encontraron registros.</p>
                                            <button
                                                onClick={() => setModalOpen(true)}
                                                className="text-blue-600 text-sm font-bold hover:underline"
                                            >
                                                → Crear nueva {tab === 'descarga' ? 'autorización' : 'entrega'}
                                            </button>
                                        </div>
                                    </td>
                                </tr>
                            ) : (
                                filtered.map((item) => {
                                    const tipo = item.ordenDetalle?.orden?.id_TipoOrden;
                                    const esDesc = tipo === TIPO_DESCARGA;
                                    const colorIcon = esDesc
                                        ? 'bg-blue-50 text-blue-600'
                                        : 'bg-emerald-50 text-emerald-600';
                                    const colorBadge = esDesc
                                        ? 'bg-blue-50 text-blue-700 border-blue-100'
                                        : 'bg-emerald-50 text-emerald-700 border-emerald-100';
                                    const esActiva = esEditable(item);
                                    return (
                                        <tr key={item.id_OrdenTransporte} className="hover:bg-slate-50/80 transition-colors group">
                                            <td className="px-8 py-5">
                                                <div className="flex items-center gap-3">
                                                    <div className={`p-2 rounded-lg ${colorIcon} group-hover:scale-110 transition-transform`}>
                                                        <FileText size={18} />
                                                    </div>
                                                    <div>
                                                        <p className="font-bold text-slate-700 text-sm">OT-{item.id_OrdenTransporte}</p>
                                                        <p className="text-[10px] text-slate-400 font-bold">PLAN ID: {item.id_OrdenDetalle}</p>
                                                    </div>
                                                </div>
                                            </td>
                                            <td className="px-8 py-5">
                                                <span className={`inline-flex items-center px-2.5 py-1 rounded-md text-[11px] font-black border uppercase tracking-tighter ${colorBadge}`}>
                                                    {item.ordenDetalle?.producto?.descripcion || 'N/A'}
                                                </span>
                                            </td>
                                            <td className="px-8 py-5 text-sm font-medium text-slate-700">
                                                {item.razonSocialTransportista?.descripcion || 'N/A'}
                                            </td>
                                            <td className="px-8 py-5 text-sm font-bold text-slate-800">
                                                {item.vehiculo?.patenteChasis || 'N/A'}
                                            </td>
                                            <td className="px-8 py-5 font-bold text-slate-800 text-sm">
                                                {item.ordenDetalle?.peso
                                                    ? `${Number(item.ordenDetalle.peso).toLocaleString('es-AR')} KG`
                                                    : '—'}
                                            </td>
                                            <td className="px-8 py-5">{getEstadoBadge(item)}</td>
                                            <td className="px-8 py-5">
                                                <div className="flex items-center gap-1.5 text-xs text-slate-500 font-medium">
                                                    <Clock size={12} />
                                                    {item.fechaPlanificacion
                                                        ? new Date(item.fechaPlanificacion).toLocaleDateString('es-AR')
                                                        : '—'}
                                                </div>
                                            </td>
                                            <td className="px-8 py-5">
                                                <div className="flex items-center gap-1.5">
                                                    {/* Ver Detalle (CU062 / CU068) */}
                                                    <button
                                                        onClick={() => setDetalleOrden(item)}
                                                        title="Ver detalle"
                                                        className="p-1.5 rounded-lg text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-colors"
                                                    >
                                                        <Eye size={16} />
                                                    </button>
                                                    {/* Modificar (CU063 / CU069) */}
                                                    {esActiva && (
                                                        <button
                                                            onClick={() => setEditarOrden(item)}
                                                            title="Modificar"
                                                            className="p-1.5 rounded-lg text-slate-400 hover:text-amber-600 hover:bg-amber-50 transition-colors"
                                                        >
                                                            <Pencil size={16} />
                                                        </button>
                                                    )}
                                                    {/* Cancelar (CU064 / CU070) */}
                                                    {esActiva && (
                                                        <button
                                                            onClick={() => setCancelarOrden(item)}
                                                            title="Cancelar orden"
                                                            className="p-1.5 rounded-lg text-slate-400 hover:text-red-600 hover:bg-red-50 transition-colors"
                                                        >
                                                            <Ban size={16} />
                                                        </button>
                                                    )}
                                                    {/* Eliminar (CU065 / CU071) */}
                                                    {esActiva && (
                                                        <button
                                                            onClick={() => handleDelete(item)}
                                                            title="Eliminar orden"
                                                            className="p-1.5 rounded-lg text-slate-400 hover:text-red-700 hover:bg-red-50 transition-colors"
                                                        >
                                                            <Trash2 size={16} />
                                                        </button>
                                                    )}
                                                </div>
                                            </td>
                                        </tr>
                                    );
                                })
                            )}
                        </tbody>
                    </table>
                </div>
            </div>

            <OrdenFormModal
                isOpen={modalOpen}
                onClose={() => setModalOpen(false)}
                onSuccess={load}
                tipoOrden={tab}
            />

            <DetalleOrdenModal
                isOpen={!!detalleOrden}
                onClose={() => setDetalleOrden(null)}
                orden={detalleOrden}
            />

            <OrdenFormModal
                isOpen={!!editarOrden}
                onClose={() => setEditarOrden(null)}
                onSuccess={load}
                orden={editarOrden}
            />

            <CancelarOrdenModal
                isOpen={!!cancelarOrden}
                onClose={() => setCancelarOrden(null)}
                onSuccess={load}
                orden={cancelarOrden}
            />
        </div>
    );
};

export default AutorizacionesPage;