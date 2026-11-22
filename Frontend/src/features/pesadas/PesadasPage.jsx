import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import {
    Scale, CheckCircle2, Loader2, AlertCircle,
    Clock, Truck, Plus, Search, Eye, X, Save,
    Printer, FileText, Check, AlertTriangle, Camera
} from 'lucide-react';
import agent, { getErrorMessage } from '../../api/agent';

// ─────────── Toast ───────────
const Toast = ({ message, type, onDone }) => {
    useEffect(() => { const t = setTimeout(onDone, 3500); return () => clearTimeout(t); }, []);
    const colors = type === 'success' ? 'bg-emerald-600' : 'bg-blue-600';
    return (
        <div className={`fixed top-6 right-6 z-[100] flex items-center gap-3 ${colors} text-white px-5 py-3.5 rounded-2xl shadow-2xl font-bold text-sm animate-in slide-in-from-top-4 duration-300`}>
            {message}
        </div>
    );
};

// ─────────── Modal Ticket de Pesada (CU079) ───────────
// Báscula de la pesada, o "Ingreso manual" si se registró sin báscula.
const origenPesada = (p) => {
    if (!p?.basculaDescripcion) return 'Ingreso manual';
    return p.esIngresoManual ? `${p.basculaDescripcion} (ingreso manual)` : p.basculaDescripcion;
};

// Se renderiza en un portal directo en <body>: el CSS de impresión (index.css) oculta todo
// lo demás y deja solo el área del ticket.
const TicketModal = ({ isOpen, onClose, ticketData }) => {
    if (!isOpen || !ticketData) return null;

    const handlePrint = () => {
        window.print();
    };

    return createPortal(
        <div className="ticket-print-root fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
            <div className="ticket-print-card bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[92vh] overflow-y-auto animate-in fade-in zoom-in-95 duration-200">
                {/* Header Ticket */}
                <div className="ticket-no-print bg-slate-900 px-6 py-5 flex justify-between items-center rounded-t-2xl text-white">
                    <div className="flex items-center gap-3">
                        <div className="p-2 bg-white/10 rounded-xl">
                            <Scale size={20} className="text-blue-400" />
                        </div>
                        <div>
                            <h3 className="text-base font-bold">Comprobante Oficial de Pesada</h3>
                            <p className="text-[11px] text-slate-400">YPF Refinería · Planta La Plata (CILP)</p>
                        </div>
                    </div>
                    <button onClick={onClose} className="text-white/70 hover:text-white">
                        <X size={20} />
                    </button>
                </div>

                {/* Body Ticket Imprimible */}
                <div className="p-6 space-y-5 font-sans" id="ticket-print-area">
                    {/* Encabezado formal */}
                    <div className="border-b border-dashed border-slate-300 pb-4 text-center">
                        <p className="text-xs uppercase tracking-widest font-black text-slate-500">Sistema de Balanzas CILP</p>
                        <h4 className="text-lg font-black text-slate-800 mt-0.5">TICKET DE PESADA #{ticketData.idPlanificacion}</h4>
                        <p className="text-xs text-slate-500 mt-1 font-mono">
                            Fecha de emisión: {ticketData.fechaEmision ? new Date(ticketData.fechaEmision).toLocaleString('es-AR') : new Date().toLocaleString('es-AR')}
                        </p>
                    </div>

                    {/* Datos de Transporte */}
                    <div className="grid grid-cols-2 gap-3 text-xs border-b border-dashed border-slate-300 pb-4">
                        <div>
                            <p className="text-slate-400 font-bold uppercase tracking-wider text-[10px]">Orden de Transporte</p>
                            <p className="font-bold text-slate-800 text-sm">OT-{ticketData.idOrdenTransporte}</p>
                        </div>
                        <div>
                            <p className="text-slate-400 font-bold uppercase tracking-wider text-[10px]">Nro. Remito</p>
                            <p className="font-bold text-slate-800 text-sm">{ticketData.nroRemito ?? '—'}</p>
                        </div>
                        <div>
                            <p className="text-slate-400 font-bold uppercase tracking-wider text-[10px]">Producto / Material</p>
                            <p className="font-bold text-blue-700 text-sm">{ticketData.producto}</p>
                        </div>
                        <div>
                            <p className="text-slate-400 font-bold uppercase tracking-wider text-[10px]">Vehículo (Chasis)</p>
                            <p className="font-bold text-slate-800 text-sm">{ticketData.vehiculo}</p>
                        </div>
                        <div>
                            <p className="text-slate-400 font-bold uppercase tracking-wider text-[10px]">Conductor</p>
                            <p className="font-semibold text-slate-700">{ticketData.conductor}</p>
                        </div>
                        <div>
                            <p className="text-slate-400 font-bold uppercase tracking-wider text-[10px]">Transportista</p>
                            <p className="font-semibold text-slate-700">{ticketData.transportista}</p>
                        </div>
                    </div>

                    {/* Detalle de Pesadas */}
                    <div className="space-y-2 border-b border-dashed border-slate-300 pb-4 font-mono text-xs">
                        {[['TARA', ticketData.pesadaTara], ['BRUTO', ticketData.pesadaBruto]].map(([titulo, p]) => (
                            <div key={titulo} className="flex justify-between items-center py-1">
                                <span className="text-slate-600 font-sans font-semibold">{titulo}:</span>
                                <div className="text-right">
                                    <span className="font-bold text-slate-800 text-sm">{p?.cantidad?.toLocaleString('es-AR')} kg</span>
                                    <p className="text-[10px] text-slate-400">
                                        {p?.fecha ? new Date(p.fecha).toLocaleTimeString('es-AR') : '—'} · {origenPesada(p)}
                                    </p>
                                    {p?.esIngresoManual && p?.motivoIngresoManual && (
                                        <p className="text-[10px] text-amber-700 font-sans">Motivo: {p.motivoIngresoManual}</p>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>

                    {/* Resumen de Peso Neto */}
                    <div className="bg-slate-50 p-4 rounded-xl space-y-2">
                        <div className="flex justify-between items-baseline">
                            <span className="text-sm font-bold text-slate-700">PESO NETO:</span>
                            <span className="text-2xl font-black text-slate-900 font-mono">
                                {Number(ticketData.pesoNeto).toLocaleString('es-AR')} <span className="text-sm font-bold text-slate-500">KG</span>
                            </span>
                        </div>
                        <div className="flex justify-between text-xs text-slate-500 pt-1 border-t border-slate-200">
                            <span>Planificado:</span>
                            <span className="font-mono font-semibold">{Number(ticketData.cantidadPlanificada).toLocaleString('es-AR')} kg</span>
                        </div>
                        <div className="flex items-center gap-1.5 pt-1">
                            {ticketData.dentroTolerancia ? (
                                <span className="inline-flex items-center gap-1 text-[11px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-md">
                                    <Check size={12} /> Dentro de la tolerancia permitida
                                </span>
                            ) : (
                                <span className="inline-flex items-center gap-1 text-[11px] font-bold text-amber-700 bg-amber-50 px-2 py-0.5 rounded-md">
                                    <AlertTriangle size={12} /> Fuera de rango de tolerancia estándar
                                </span>
                            )}
                        </div>
                    </div>

                    {/* Acciones */}
                    <div className="ticket-no-print flex gap-3 pt-2">
                        <button
                            onClick={onClose}
                            className="flex-1 py-2.5 rounded-xl border border-slate-200 text-slate-700 font-semibold text-sm hover:bg-slate-50 transition-colors"
                        >
                            Cerrar
                        </button>
                        <button
                            onClick={handlePrint}
                            className="flex-1 py-2.5 rounded-xl bg-blue-600 text-white font-bold text-sm hover:bg-blue-700 transition-colors shadow-lg shadow-blue-500/20 flex items-center justify-center gap-2"
                        >
                            <Printer size={16} /> Imprimir Comprobante
                        </button>
                    </div>
                </div>
            </div>
        </div>,
        document.body
    );
};

// ─────────── Modal: Registrar Pesada (CU078) ───────────
const LARGO_MAXIMO_MOTIVO = 300;   // t_Pesada.MotivoIngresoManual_Pesada

const FORM_VACIO = {
    Id_Planificacion: '',
    Cantidad: '',
    Id_Bascula: '',
    EsIngresoManual: false,
    MotivoIngresoManual: '',
};

const RegistrarPesadaModal = ({ isOpen, onClose, onSuccess, initialPlanId }) => {
    const [planificaciones, setPlanificaciones] = useState([]);
    const [basculas, setBasculas] = useState([]);
    const [formData, setFormData] = useState(FORM_VACIO);
    // Peso que leyó la báscula para la báscula y la orden elegidas. Si el peso del formulario
    // no es exactamente esta lectura (se tipeó o se editó), la pesada es manual.
    const [lectura, setLectura] = useState(null);
    const [loading, setLoading] = useState(false);
    const [loadingOpts, setLoadingOpts] = useState(true);
    const [leyendo, setLeyendo] = useState(false);

    useEffect(() => {
        if (!isOpen) return;

        setLectura(null);
        setFormData({ ...FORM_VACIO, Id_Planificacion: initialPlanId ? String(initialPlanId) : '' });
        setLoadingOpts(true);
        Promise.all([
            agent.Planificaciones.pendientes(),
            agent.Basculas.list(),
        ]).then(([plans, bascs]) => {
            setPlanificaciones(plans);
            setBasculas(bascs);
            // Báscula por defecto: la primera activa (no un id fijo).
            setFormData(prev => ({
                ...prev,
                Id_Bascula: prev.Id_Bascula || (bascs.length > 0 ? String(bascs[0].id_Bascula) : ''),
            }));
        }).catch(err => {
            alert(getErrorMessage(err, 'No se pudieron cargar las órdenes y las básculas.'));
        }).finally(() => setLoadingOpts(false));
    }, [isOpen, initialPlanId]);

    if (!isOpen) return null;

    const coincideLectura = lectura != null && formData.Cantidad !== '' && Number(formData.Cantidad) === lectura;
    // Peso tipeado o editado: la pesada es manual sí o sí.
    const forzadoManual = formData.Cantidad !== '' && !coincideLectura;
    const esManual = forzadoManual || formData.EsIngresoManual;

    const handleChange = e => {
        const { name, value, type, checked } = e.target;
        // La lectura corresponde a una báscula y a una orden: si cambia alguna, deja de valer.
        if (name === 'Id_Planificacion' || name === 'Id_Bascula') setLectura(null);
        setFormData(prev => ({ ...prev, [name]: type === 'checkbox' ? checked : value }));
    };

    // Lee el peso desde la báscula (Device Connector). Mientras no se edite, la pesada es automática.
    const handleLeerBascula = async () => {
        if (!formData.Id_Planificacion) {
            alert('Seleccione primero la Orden de Transporte.');
            return;
        }
        if (!formData.Id_Bascula) {
            alert('Seleccione la báscula a leer.');
            return;
        }
        setLeyendo(true);
        try {
            const resultado = await agent.Hardware.leerPeso(formData.Id_Bascula, formData.Id_Planificacion);
            setLectura(resultado.pesoKg);
            setFormData(prev => ({ ...prev, Cantidad: String(resultado.pesoKg), EsIngresoManual: false, MotivoIngresoManual: '' }));
        } catch (err) {
            alert(getErrorMessage(err, 'No se pudo leer la báscula. Puede usar el ingreso manual.'));
        } finally {
            setLeyendo(false);
        }
    };

    const handleSubmit = async e => {
        e.preventDefault();
        const motivo = formData.MotivoIngresoManual.trim();
        if (esManual && !motivo) {
            alert('El ingreso manual requiere indicar el motivo.');
            return;
        }
        if (!esManual && !formData.Id_Bascula) {
            alert('Una pesada automática requiere la báscula que leyó el peso.');
            return;
        }

        setLoading(true);
        try {
            const result = await agent.Pesadas.create({
                Id_Planificacion: parseInt(formData.Id_Planificacion),
                Cantidad: parseFloat(formData.Cantidad),
                Id_Bascula: formData.Id_Bascula ? parseInt(formData.Id_Bascula) : null,
                EsIngresoManual: esManual,
                MotivoIngresoManual: esManual ? motivo : null,
            });

            onSuccess({
                completada: result?.pesoNeto != null,
                tipoRegistrado: result?.id_TipoPesada,
                pesoNeto: result?.pesoNeto ?? null,
            });
            onClose();
        } catch (err) {
            console.error(err);
            alert(getErrorMessage(err, 'Error al registrar la pesada.'));
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
            <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg animate-in fade-in zoom-in-95 duration-200">
                <div className="bg-linear-to-r from-blue-700 to-blue-600 px-6 py-5 flex justify-between items-center rounded-t-2xl">
                    <div className="flex items-center gap-3">
                        <div className="p-2 bg-white/10 rounded-xl"><Scale size={20} className="text-white" /></div>
                        <h3 className="text-lg font-bold text-white">Registrar Pesada</h3>
                    </div>
                    <button onClick={onClose} className="text-white/70 hover:text-white">
                        <X size={22} />
                    </button>
                </div>
                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                            Orden de Transporte
                        </label>
                        <select
                            name="Id_Planificacion"
                            value={formData.Id_Planificacion}
                            onChange={handleChange}
                            className="w-full p-2.5 border border-slate-200 rounded-xl bg-white text-sm font-medium focus:ring-2 focus:ring-blue-500/20 focus:border-blue-400 outline-none"
                            required
                            disabled={loadingOpts}
                        >
                            <option value="">Seleccione una Orden...</option>
                            {planificaciones.map(p => {
                                const prod = p.ordenTransporte?.ordenDetalle?.producto?.descripcion || 'Producto';
                                const pat = p.ordenTransporte?.vehiculo?.patenteChasis || 'Vehículo';
                                const kg = p.cantidadKG ?? p.cantidadPlanificada ?? '?';
                                return (
                                    <option key={p.id_Planificacion} value={p.id_Planificacion}>
                                        OT-{p.id_OrdenTransporte} | {prod} | {pat} | {Number(kg).toLocaleString('es-AR')} kg
                                    </option>
                                );
                            })}
                        </select>
                        {!loadingOpts && planificaciones.length === 0 && (
                            <p className="text-xs text-amber-600 mt-1 flex items-center gap-1">
                                <AlertCircle size={12} /> No hay órdenes pendientes de pesaje.
                            </p>
                        )}
                    </div>

                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <div className="flex items-center justify-between mb-1.5">
                                <label className="block text-sm font-semibold text-slate-700">Peso (kg)</label>
                                <button
                                    type="button"
                                    onClick={handleLeerBascula}
                                    disabled={leyendo || !formData.Id_Bascula}
                                    className="text-xs font-bold text-blue-600 hover:text-blue-800 disabled:opacity-40 flex items-center gap-1"
                                >
                                    {leyendo ? <Loader2 size={12} className="animate-spin" /> : <Scale size={12} />}
                                    Leer de báscula
                                </button>
                            </div>
                            <input
                                type="number"
                                name="Cantidad"
                                step="0.01"
                                min="0.01"
                                value={formData.Cantidad}
                                onChange={handleChange}
                                placeholder="Ej: 24500"
                                className="w-full p-2.5 border border-slate-200 rounded-xl text-sm font-medium focus:ring-2 focus:ring-blue-500/20 focus:border-blue-400 outline-none"
                                required
                            />
                            {coincideLectura && (
                                <p className="text-[11px] text-emerald-700 mt-1">Lectura de la báscula: pesada automática.</p>
                            )}
                            {forzadoManual && (
                                <p className="text-[11px] text-amber-700 mt-1">El peso no es una lectura de la báscula: queda como ingreso manual.</p>
                            )}
                        </div>
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">Báscula</label>
                            <select
                                name="Id_Bascula"
                                value={formData.Id_Bascula}
                                onChange={handleChange}
                                className="w-full p-2.5 border border-slate-200 rounded-xl bg-white text-sm font-medium focus:ring-2 focus:ring-blue-500/20 focus:border-blue-400 outline-none"
                            >
                                {basculas.map(b => (
                                    <option key={b.id_Bascula} value={b.id_Bascula}>
                                        {b.descripcion || `Báscula #${b.id_Bascula}`}
                                    </option>
                                ))}
                                <option value="">Sin báscula (solo ingreso manual)</option>
                            </select>
                        </div>
                    </div>

                    <div className="flex items-center gap-2">
                        <input
                            type="checkbox"
                            name="EsIngresoManual"
                            id="chkManual"
                            checked={esManual}
                            disabled={forzadoManual}
                            onChange={handleChange}
                            className="w-4 h-4 text-blue-600 rounded border-slate-300 disabled:opacity-60"
                        />
                        <label htmlFor="chkManual" className="text-sm font-semibold text-slate-700">Ingreso Manual</label>
                    </div>

                    {esManual && (
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">Motivo *</label>
                            <textarea
                                name="MotivoIngresoManual"
                                value={formData.MotivoIngresoManual}
                                onChange={handleChange}
                                rows={2}
                                maxLength={LARGO_MAXIMO_MOTIVO}
                                required
                                className="w-full p-2.5 border border-slate-200 rounded-xl text-sm font-medium focus:ring-2 focus:ring-blue-500/20 focus:border-blue-400 outline-none resize-none"
                                placeholder="Motivo del ingreso manual (obligatorio)..."
                            />
                        </div>
                    )}

                    <div className="flex gap-3 pt-2">
                        <button
                            type="button"
                            onClick={onClose}
                            className="flex-1 py-2.5 rounded-xl border border-slate-200 text-slate-700 font-semibold text-sm hover:bg-slate-50 transition-colors"
                        >
                            Cancelar
                        </button>
                        <button
                            type="submit"
                            disabled={loading || loadingOpts}
                            className="flex-1 py-2.5 rounded-xl bg-blue-600 text-white font-bold text-sm hover:bg-blue-700 transition-colors flex items-center justify-center gap-2 shadow-lg shadow-blue-500/20"
                        >
                            {loading ? <Loader2 size={16} className="animate-spin" /> : <Save size={16} />}
                            {loading ? 'Registrando...' : 'Registrar Peso'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

// ─────────── Página principal ───────────

const PesadasPage = () => {
    const [pesadas, setPesadas] = useState([]);
    const [planificaciones, setPlanificaciones] = useState([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');
    const [modalOpen, setModalOpen] = useState(false);
    const [toast, setToast] = useState(null);

    // CU077: Búsqueda por patente
    const [patenteSearch, setPatenteSearch] = useState('');
    const [searchingPatente, setSearchingPatente] = useState(false);
    const [otFound, setOtFound] = useState(null);
    const [searchPatenteError, setSearchPatenteError] = useState('');
    const [selectedPlanForModal, setSelectedPlanForModal] = useState('');

    // CU079: Ticket modal
    const [ticketData, setTicketData] = useState(null);
    const [loadingTicket, setLoadingTicket] = useState(false);

    const load = async () => {
        setLoading(true);
        try {
            const [pes, plans] = await Promise.all([
                agent.Pesadas.list(),
                agent.Planificaciones.list(),
            ]);
            setPesadas(pes);
            setPlanificaciones(plans);
        } catch (e) {
            console.error(e);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { load(); }, []);

    const handleSuccess = ({ completada, tipoRegistrado, pesoNeto }) => {
        load();
        // La OT encontrada por patente pudo haber cambiado de estado: se descarta el resultado.
        setOtFound(null);
        if (completada) {
            setToast({ message: `✓ Orden completada. Peso neto: ${Number(pesoNeto).toLocaleString('es-AR')} kg.`, type: 'success' });
        } else {
            const registrada = tipoRegistrado === 2 ? 'BRUTO' : 'TARA';
            const pendiente = tipoRegistrado === 2 ? 'TARA' : 'BRUTO';
            setToast({ message: `✓ Pesada ${registrada} registrada. Aguardando pesada ${pendiente}.`, type: 'info' });
        }
    };

    // CU077: Buscar OT asignada al vehículo por patente
    const handleSearchPatente = async (e) => {
        e.preventDefault();
        if (!patenteSearch.trim()) return;

        setSearchingPatente(true);
        setSearchPatenteError('');
        setOtFound(null);

        try {
            const ot = await agent.OrdenesTransporte.byPatente(patenteSearch.trim());
            setOtFound(ot);
        } catch (err) {
            console.error(err);
            setSearchPatenteError(getErrorMessage(err, `No se pudo buscar la orden de la patente "${patenteSearch.toUpperCase()}".`));
        } finally {
            setSearchingPatente(false);
        }
    };

    // CU079: Emitir Ticket
    const handleEmitirTicket = async (idPlanificacion) => {
        setLoadingTicket(true);
        try {
            const ticket = await agent.Pesadas.ticket(idPlanificacion);
            setTicketData(ticket);
        } catch (err) {
            console.error(err);
            alert(getErrorMessage(err, 'No se pudo generar el ticket. Verifique que la orden tenga registradas ambas pesadas (TARA y BRUTO).'));
        } finally {
            setLoadingTicket(false);
        }
    };

    // La cámara OCR detecta un camión y el backend devuelve la OT pendiente que le corresponde:
    // se muestra esa OT (no se vuelve a buscar por patente).
    const handleDetectarCamara = async () => {
        setSearchingPatente(true);
        setSearchPatenteError('');
        setOtFound(null);
        try {
            const estado = await agent.Hardware.estado();
            const camara = estado.dispositivos?.find(d => d.estado === 'ONLINE');
            if (!camara) throw new Error('Sin cámara operativa');
            const deteccion = await agent.Hardware.detectarPatente(camara.id_DispositivoDetector);
            setPatenteSearch(deteccion.patente);
            const ot = await agent.OrdenesTransporte.details(deteccion.id_OrdenTransporte);
            setOtFound(ot);
        } catch (err) {
            setSearchPatenteError(getErrorMessage(err, 'No se pudo detectar un vehículo con la cámara.'));
        } finally {
            setSearchingPatente(false);
        }
    };

    const handleAbrirPesadaParaOT = (ot) => {
        const plan = planificaciones.find(p => p.id_OrdenTransporte === ot.id_OrdenTransporte);
        if (plan) {
            setSelectedPlanForModal(plan.id_Planificacion);
        } else {
            setSelectedPlanForModal('');
        }
        setModalOpen(true);
    };

    const getPlanInfo = (idPlan) => {
        const p = planificaciones.find(pl => pl.id_Planificacion === idPlan);
        if (!p) return { producto: 'N/A', patente: 'N/A', otId: idPlan };
        return {
            producto: p.ordenTransporte?.ordenDetalle?.producto?.descripcion ?? 'N/A',
            patente: p.ordenTransporte?.vehiculo?.patenteChasis ?? 'N/A',
            otId: p.id_OrdenTransporte,
        };
    };

    // Agrupación de pesadas por planificación para calcular peso neto
    const pesoNetoByPlan = React.useMemo(() => {
        const map = {};
        const byPlan = {};
        pesadas.forEach(p => {
            if (!byPlan[p.id_Planificacion]) byPlan[p.id_Planificacion] = [];
            byPlan[p.id_Planificacion].push(p);
        });
        Object.entries(byPlan).forEach(([idPlan, pList]) => {
            const tara = pList.find(p => p.id_TipoPesada === 1);
            const bruto = pList.find(p => p.id_TipoPesada === 2);
            if (tara && bruto) {
                map[idPlan] = bruto.cantidad - tara.cantidad;
            }
        });
        return map;
    }, [pesadas]);

    const filtered = pesadas.filter(p => {
        const info = getPlanInfo(p.id_Planificacion);
        const q = search.toLowerCase();
        return (
            String(p.id_Pesada).includes(q) ||
            info.producto.toLowerCase().includes(q) ||
            info.patente.toLowerCase().includes(q)
        );
    });

    const tipoBadge = (tipo) => {
        if (tipo === 1) return <span className="px-2 py-1 rounded-md text-[11px] font-bold bg-slate-100 text-slate-600">TARA</span>;
        return <span className="px-2 py-1 rounded-md text-[11px] font-bold bg-blue-100 text-blue-700">BRUTO</span>;
    };

    return (
        <div className="space-y-6">
            {toast && (
                <Toast
                    message={toast.message}
                    type={toast.type}
                    onDone={() => setToast(null)}
                />
            )}

            {/* Header */}
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <div>
                    <h2 className="text-2xl font-bold text-slate-800">Gestión de Pesadas</h2>
                    <p className="text-slate-500 text-sm mt-0.5">Operaciones en Báscula · CU077 · CU078 · CU079</p>
                </div>
                <button
                    onClick={() => { setSelectedPlanForModal(''); setModalOpen(true); }}
                    className="bg-blue-600 text-white px-5 py-2.5 rounded-xl font-bold flex items-center gap-2 hover:bg-blue-700 shadow-lg shadow-blue-500/20 transition-all active:scale-95"
                >
                    <Plus size={18} /> Nueva Pesada
                </button>
            </div>

            {/* CU077: Panel de Búsqueda de Orden por Patente del Vehículo */}
            <div className="bg-white rounded-2xl shadow-sm border border-slate-100 p-5">
                <div className="flex items-center gap-2 mb-3">
                    <Truck size={18} className="text-blue-600" />
                    <h3 className="font-bold text-sm text-slate-800 uppercase tracking-wider">
                        Consultar Orden Asignada al Vehículo (CU077)
                    </h3>
                </div>
                <form onSubmit={handleSearchPatente} className="flex flex-col sm:flex-row gap-3">
                    <div className="relative flex-1 max-w-md">
                        <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={16} />
                        <input
                            type="text"
                            placeholder="Ingrese patente (ej: AA123BB o ABC123DE)..."
                            value={patenteSearch}
                            onChange={(e) => setPatenteSearch(e.target.value.toUpperCase())}
                            className="w-full bg-slate-50 border border-slate-200 rounded-xl py-2 pl-9 pr-4 text-sm font-mono font-bold focus:ring-2 focus:ring-blue-500/20 focus:border-blue-300 outline-none uppercase"
                        />
                    </div>
                    <button
                        type="submit"
                        disabled={searchingPatente || !patenteSearch.trim()}
                        className="bg-slate-900 text-white px-5 py-2 rounded-xl text-sm font-bold hover:bg-slate-800 transition-colors flex items-center justify-center gap-2 disabled:opacity-50"
                    >
                        {searchingPatente ? <Loader2 size={16} className="animate-spin" /> : <Search size={16} />}
                        Buscar por Patente
                    </button>
                    <button
                        type="button"
                        onClick={handleDetectarCamara}
                        disabled={searchingPatente}
                        className="bg-white text-slate-700 border border-slate-200 px-5 py-2 rounded-xl text-sm font-bold hover:bg-slate-50 transition-colors flex items-center justify-center gap-2 disabled:opacity-50"
                    >
                        <Camera size={16} />
                        Detectar con cámara
                    </button>
                </form>

                {/* Resultado de búsqueda por Patente */}
                {otFound && (
                    <div className="mt-4 p-4 rounded-xl bg-blue-50/70 border border-blue-100 animate-in fade-in duration-300 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                        <div className="space-y-1 text-xs">
                            <div className="flex items-center gap-2">
                                <span className="bg-blue-600 text-white px-2 py-0.5 rounded font-mono font-black text-xs">
                                    {otFound.vehiculo?.patenteChasis}
                                </span>
                                <span className="font-bold text-slate-800 text-sm">
                                    OT-{otFound.id_OrdenTransporte}
                                </span>
                                <span className="text-slate-500 font-medium">·</span>
                                <span className="font-bold text-blue-700 uppercase">
                                    {otFound.ordenDetalle?.producto?.descripcion || 'Material'}
                                </span>
                            </div>
                            <p className="text-slate-600">
                                <strong>Conductor:</strong> {otFound.conductor ? `${otFound.conductor.apellido}, ${otFound.conductor.nombre}` : 'N/A'} · <strong>Transportista:</strong> {otFound.razonSocialTransportista?.descripcion || 'N/A'}
                            </p>
                            <p className="text-slate-500">
                                <strong>Cantidad Planificada:</strong> {otFound.ordenDetalle?.peso ? `${Number(otFound.ordenDetalle.peso).toLocaleString('es-AR')} KG` : '—'}
                            </p>
                        </div>
                        <button
                            onClick={() => handleAbrirPesadaParaOT(otFound)}
                            className="bg-blue-600 text-white px-4 py-2 rounded-xl text-xs font-bold hover:bg-blue-700 transition-colors shadow-md shadow-blue-500/20 shrink-0 flex items-center gap-1.5"
                        >
                            <Scale size={14} /> Registrar Pesada para esta OT →
                        </button>
                    </div>
                )}

                {searchPatenteError && (
                    <div className="mt-3 text-xs text-amber-700 bg-amber-50 border border-amber-100 p-3 rounded-xl flex items-center gap-2">
                        <AlertCircle size={15} />
                        <span>{searchPatenteError}</span>
                    </div>
                )}
            </div>

            {/* Listado y Filtro de Pesadas */}
            <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
                <div className="relative max-w-sm w-full">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" size={16} />
                    <input
                        type="text"
                        placeholder="Filtrar por producto, patente o ID..."
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                        className="w-full bg-slate-50 border border-slate-200 rounded-xl py-2 pl-9 pr-4 text-sm font-medium focus:ring-2 focus:ring-blue-500/20 focus:border-blue-300 outline-none"
                    />
                </div>
            </div>

            {/* Table */}
            <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
                <div className="overflow-x-auto">
                    <table className="w-full text-left">
                        <thead>
                            <tr className="text-slate-400 text-[11px] uppercase tracking-widest bg-slate-50/70 border-b border-slate-100">
                                <th className="px-6 py-4 font-bold">ID Pesada</th>
                                <th className="px-6 py-4 font-bold">OT</th>
                                <th className="px-6 py-4 font-bold">Producto</th>
                                <th className="px-6 py-4 font-bold">Vehículo</th>
                                <th className="px-6 py-4 font-bold">Tipo</th>
                                <th className="px-6 py-4 font-bold">Peso (kg)</th>
                                <th className="px-6 py-4 font-bold">Peso Neto</th>
                                <th className="px-6 py-4 font-bold">Báscula</th>
                                <th className="px-6 py-4 font-bold">Fecha / Hora</th>
                                <th className="px-6 py-4 font-bold">Modo</th>
                                <th className="px-6 py-4 font-bold">Ticket</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-50">
                            {loading ? (
                                [1, 2, 3, 4].map(i => (
                                    <tr key={i} className="animate-pulse">
                                        <td colSpan={11} className="px-6 py-5">
                                            <div className="h-4 bg-slate-100 rounded-full w-3/4" />
                                        </td>
                                    </tr>
                                ))
                            ) : filtered.length === 0 ? (
                                <tr>
                                    <td colSpan={11} className="px-6 py-16 text-center">
                                        <div className="flex flex-col items-center gap-2 text-slate-400">
                                            <Scale size={36} className="opacity-20" />
                                            <p className="font-medium text-sm">No se encontraron pesadas</p>
                                        </div>
                                    </td>
                                </tr>
                            ) : (
                                filtered.map(p => {
                                    const { producto, patente, otId } = getPlanInfo(p.id_Planificacion);
                                    const neto = pesoNetoByPlan[p.id_Planificacion];
                                    return (
                                        <tr key={p.id_Pesada} className="hover:bg-slate-50/80 transition-colors group">
                                            <td className="px-6 py-4">
                                                <span className="font-bold text-slate-500 text-sm">#{p.id_Pesada}</span>
                                            </td>
                                            <td className="px-6 py-4">
                                                <div className="flex items-center gap-2">
                                                    <div className="p-1.5 bg-blue-50 rounded-lg">
                                                        <Truck size={14} className="text-blue-600" />
                                                    </div>
                                                    <span className="text-xs font-bold text-slate-600">
                                                        OT-{otId}
                                                    </span>
                                                </div>
                                            </td>
                                            <td className="px-6 py-4 text-sm font-medium text-slate-700">{producto}</td>
                                            <td className="px-6 py-4 text-sm font-medium text-slate-700">{patente}</td>
                                            <td className="px-6 py-4">{tipoBadge(p.id_TipoPesada)}</td>
                                            <td className="px-6 py-4">
                                                <span className="font-bold text-slate-800 text-sm font-mono">
                                                    {p.cantidad?.toLocaleString('es-AR')} kg
                                                </span>
                                            </td>
                                            <td className="px-6 py-4">
                                                {neto != null ? (
                                                    <span className="font-bold text-emerald-700 text-sm font-mono">
                                                        {Number(neto).toLocaleString('es-AR')} kg
                                                    </span>
                                                ) : (
                                                    <span className="text-slate-300 text-sm font-medium">—</span>
                                                )}
                                            </td>
                                            <td className="px-6 py-4">
                                                <div className="flex items-center gap-1.5">
                                                    <div className="w-1.5 h-1.5 rounded-full bg-green-400" />
                                                    <span className="text-xs font-bold text-slate-600">
                                                        #{p.id_Bascula ?? 'manual'}
                                                    </span>
                                                </div>
                                            </td>
                                            <td className="px-6 py-4 text-xs font-medium text-slate-500">
                                                {p.fecha ? new Date(p.fecha).toLocaleString('es-AR') : '—'}
                                            </td>
                                            <td className="px-6 py-4">
                                                {p.esIngresoManual
                                                    ? <span className="px-2 py-1 rounded-md text-[11px] font-bold bg-amber-50 text-amber-700">MANUAL</span>
                                                    : <span className="px-2 py-1 rounded-md text-[11px] font-bold bg-emerald-50 text-emerald-700">AUTO</span>
                                                }
                                            </td>
                                            <td className="px-6 py-4">
                                                {neto != null ? (
                                                    <button
                                                        onClick={() => handleEmitirTicket(p.id_Planificacion)}
                                                        disabled={loadingTicket}
                                                        title="Ver / Imprimir Ticket de Pesada (CU079)"
                                                        className="p-1.5 rounded-lg text-slate-500 hover:text-blue-600 hover:bg-blue-50 transition-colors flex items-center gap-1 text-xs font-bold"
                                                    >
                                                        <Printer size={15} /> Ticket
                                                    </button>
                                                ) : (
                                                    <span className="text-[11px] text-slate-300 font-medium italic">En curso</span>
                                                )}
                                            </td>
                                        </tr>
                                    );
                                })
                            )}
                        </tbody>
                    </table>
                </div>
            </div>

            <RegistrarPesadaModal
                isOpen={modalOpen}
                onClose={() => setModalOpen(false)}
                onSuccess={handleSuccess}
                initialPlanId={selectedPlanForModal}
            />

            <TicketModal
                isOpen={!!ticketData}
                onClose={() => setTicketData(null)}
                ticketData={ticketData}
            />
        </div>
    );
};

export default PesadasPage;