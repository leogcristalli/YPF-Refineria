import React, { useState, useEffect } from 'react';
import {
    X, Save, Loader2, AlertCircle, Calendar, FileText, Eye, Ban, Database, MapPin, CalendarClock
} from 'lucide-react';
import agent, { getErrorMessage } from '../../api/agent';

// Modales de órdenes compartidos por Planificación Logística (Autorizaciones de Descarga y
// Órdenes de Entrega) y por Órdenes de Transporte: un solo formulario de alta y modificación.

// Valores de los datos de sistema.
export const TIPO_ENTREGA = 1;            // t_TipoOrden
export const TIPO_DESCARGA = 2;
const ORDEN_DEFINITIVO = 2;               // t_EstadoOrden
const ORDEN_ANULADO = 5;
const OT_EN_EJECUCION = 3;                // t_EstadoOrdenTransporte
const OT_EJECUTADO = 5;

const hoyISO = () => {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};
// Fecha del input (AAAA-MM-DD) como fecha local, sin corrimiento por zona horaria.
const fechaParaApi = (fecha) => `${fecha}T00:00:00`;
const formatoFecha = (valor) => (valor ? new Date(valor).toLocaleDateString('es-AR') : '—');

// Estado visible de una orden: anulada o cerrada según la orden; si no, el de la OT.
export const estadoDe = (item) => {
    const orden = item.ordenDetalle?.orden;
    if (orden?.id_EstadoOrden === ORDEN_ANULADO) return { texto: 'Anulado', tono: 'rojo' };
    if (orden?.id_EstadoOrden === ORDEN_DEFINITIVO || orden?.fechaCierre) return { texto: 'Definitivo (cerrada)', tono: 'verde' };
    return { texto: item.estadoOrdenTransporte?.descripcion ?? orden?.estadoOrden?.descripcion ?? 'Planificado', tono: 'azul' };
};

// Se puede modificar, cancelar o replanificar: abierta y sin pesadas (ni en ejecución ni ejecutada).
export const esEditable = (item) => {
    const orden = item.ordenDetalle?.orden;
    return orden?.activo !== false && !orden?.fechaCierre && item.activo !== false
        && item.id_EstadoOrdenTransporte !== OT_EN_EJECUCION && item.id_EstadoOrdenTransporte !== OT_EJECUTADO;
};

export const MensajeError = ({ mensaje }) => mensaje ? (
    <div role="alert" className="text-sm text-red-700 bg-red-50 border border-red-100 rounded-xl p-3 flex items-start gap-2">
        <AlertCircle size={16} className="shrink-0 mt-0.5" />
        <span>{mensaje}</span>
    </div>
) : null;

const claseCampo = 'w-full p-2.5 border border-slate-200 rounded-xl text-sm font-medium focus:ring-2 focus:ring-blue-500/20 focus:border-blue-400 outline-none';
const claseSelect = `${claseCampo} bg-white`;

// ─────────── Alta y modificación (CU061 / CU063 / CU067 / CU069 / CU073 / CU075) ───────────
// Alta: orden = null y tipoOrden 'descarga' | 'despacho' (o null para elegirlo en el formulario).
// Modificación: orden = la OT a modificar. La fecha no se edita: se cambia con Replanificar.
export const OrdenFormModal = ({ isOpen, onClose, onSuccess, orden, tipoOrden }) => {
    const esEdicion = !!orden;
    const [opciones, setOpciones] = useState({ productos: [], razones: [], vehiculos: [], conductores: [], tanques: [], puestos: [] });
    const [loading, setLoading] = useState(false);
    const [loadingOpts, setLoadingOpts] = useState(true);
    const [error, setError] = useState('');
    const [formData, setFormData] = useState({});

    useEffect(() => {
        if (!isOpen) return;
        setError('');
        const detalle = orden?.ordenDetalle;
        setFormData(esEdicion ? {
            tipo: detalle?.orden?.id_TipoOrden === TIPO_DESCARGA ? 'descarga' : 'despacho',
            id_PuestoExpedicionRecepcion: detalle?.orden?.id_PuestoExpedicionRecepcion ?? '',
            id_Producto: detalle?.id_Producto ?? '',
            id_RazonSocialCliente: detalle?.id_RazonSocialClienteProveedor ?? '',
            id_RazonSocialTransportista: orden.id_RazonSocialTransportista ?? '',
            id_Vehiculo: orden.id_Vehiculo ?? '',
            id_Conductor: orden.id_Conductor ?? '',
            nroRemito: detalle?.nroRemito ?? '',
            cantidad: detalle?.peso ?? '',
            id_Tanque: detalle?.tanquesAlmacenamiento?.[0]?.id_Tanque ?? '',
            fecha: (orden.fechaPlanificacion ?? '').split('T')[0],
        } : {
            tipo: tipoOrden ?? '',
            id_PuestoExpedicionRecepcion: '', id_Producto: '', id_RazonSocialCliente: '', id_RazonSocialTransportista: '',
            id_Vehiculo: '', id_Conductor: '', nroRemito: '', cantidad: '', id_Tanque: '', fecha: hoyISO(),
        });
        setLoadingOpts(true);
        Promise.all([
            agent.Productos.list(), agent.RazonesSociales.list(), agent.Vehiculos.list(),
            agent.Conductores.list(), agent.Tanques.list(), agent.Puestos.list(),
        ]).then(([productos, razones, vehiculos, conductores, tanques, puestos]) => {
            setOpciones({ productos, razones, vehiculos, conductores, tanques, puestos });
        }).catch(err => setError(getErrorMessage(err, 'No se pudieron cargar las opciones del formulario.')))
            .finally(() => setLoadingOpts(false));
    }, [isOpen, orden, tipoOrden, esEdicion]);

    if (!isOpen) return null;

    const esDescarga = formData.tipo === 'descarga';
    const tipoElegido = formData.tipo === 'descarga' || formData.tipo === 'despacho';
    const transportistas = opciones.razones.filter(r => r.esTransportista);

    const handleChange = e => {
        const { name, value } = e.target;
        setError('');
        setFormData(prev => ({ ...prev, [name]: value }));
    };

    // Capacidad del tanque de destino (litros; la cantidad está en kg y se convierte con la
    // densidad). Al modificar, el volumen que ya reservaba la orden en ese tanque se libera.
    const tanque = opciones.tanques.find(t => String(t.id_Tanque) === String(formData.id_Tanque));
    const producto = opciones.productos.find(p => String(p.id_Producto) === String(formData.id_Producto));
    const litros = producto?.densidad > 0 && formData.cantidad ? parseFloat(formData.cantidad) / producto.densidad : null;
    const reservaActual = orden?.ordenDetalle?.tanquesAlmacenamiento?.[0];
    const disponible = tanque
        ? tanque.capacidadDisponible + (esEdicion && esDescarga && reservaActual?.id_Tanque === tanque.id_Tanque ? reservaActual.volumen : 0)
        : null;
    const capacidadExcedida = esDescarga && tanque && litros != null && litros > disponible;

    const handleSubmit = async e => {
        e.preventDefault();
        setError('');
        if (capacidadExcedida) {
            setError(`Capacidad insuficiente en el tanque ${tanque.codigo}: disponible ${Math.floor(disponible).toLocaleString('es-AR')} L, la carga requiere ${Math.ceil(litros).toLocaleString('es-AR')} L.`);
            return;
        }
        setLoading(true);
        try {
            const payload = {
                id_TipoOrden: esDescarga ? TIPO_DESCARGA : TIPO_ENTREGA,
                id_PuestoExpedicionRecepcion: parseInt(formData.id_PuestoExpedicionRecepcion),
                id_Producto: parseInt(formData.id_Producto),
                id_RazonSocialCliente: parseInt(formData.id_RazonSocialCliente),
                id_RazonSocialTransportista: parseInt(formData.id_RazonSocialTransportista),
                id_Vehiculo: parseInt(formData.id_Vehiculo),
                id_Conductor: parseInt(formData.id_Conductor),
                nroRemito: parseInt(formData.nroRemito),
                cantidad: parseFloat(formData.cantidad),
                id_Tanque: formData.id_Tanque ? parseInt(formData.id_Tanque) : null,
                fecha: fechaParaApi(formData.fecha),
            };
            if (esEdicion) await agent.OrdenesTransporte.update(orden.id_OrdenTransporte, payload);
            else await agent.OrdenesTransporte.create(payload);
            onSuccess();
            onClose();
        } catch (err) {
            setError(getErrorMessage(err, 'No se pudo guardar la orden.'));
        } finally {
            setLoading(false);
        }
    };

    const titulo = !tipoElegido ? 'Nueva Orden'
        : `${esEdicion ? 'Modificar' : 'Nueva'} ${esDescarga ? 'Autorización de Descarga' : 'Orden de Entrega'}`;
    const color = esDescarga ? 'from-blue-700 to-blue-600' : 'from-emerald-700 to-emerald-600';

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
            <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto animate-in fade-in zoom-in-95 duration-200">
                <div className={`px-6 py-5 flex justify-between items-center rounded-t-2xl sticky top-0 z-10 bg-linear-to-r ${color}`}>
                    <div className="flex items-center gap-3">
                        <div className="p-2 bg-white/10 rounded-xl"><FileText size={20} className="text-white" /></div>
                        <div>
                            <h3 className="text-lg font-bold text-white">{titulo}</h3>
                            {esEdicion && <p className="text-xs text-white/70">OT-{orden.id_OrdenTransporte}</p>}
                        </div>
                    </div>
                    <button onClick={onClose} className="text-white/70 hover:text-white"><X size={22} /></button>
                </div>

                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    {/* Tipo: solo al crear desde Órdenes de Transporte */}
                    {!esEdicion && tipoOrden == null && (
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">Tipo de orden *</label>
                            <select name="tipo" value={formData.tipo} onChange={handleChange} className={claseSelect} required>
                                <option value="">Seleccione...</option>
                                <option value="descarga">Autorización de Descarga (recepción)</option>
                                <option value="despacho">Orden de Entrega (despacho)</option>
                            </select>
                        </div>
                    )}

                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                                <Calendar size={13} className="inline mr-1" />Fecha planificada
                            </label>
                            {esEdicion ? (
                                <>
                                    <p className="p-2.5 text-sm font-medium text-slate-700 bg-slate-50 rounded-xl border border-slate-100">{formatoFecha(orden.fechaPlanificacion)}</p>
                                    <p className="text-[11px] text-slate-400 mt-1">Para cambiarla, use Replanificar en Órdenes de Transporte.</p>
                                </>
                            ) : (
                                <input type="date" name="fecha" value={formData.fecha} min={hoyISO()} onChange={handleChange} className={claseCampo} required />
                            )}
                        </div>
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">Cantidad (KG) *</label>
                            <input type="number" name="cantidad" value={formData.cantidad} onChange={handleChange} min="0.01" step="0.01" placeholder="Ej: 30000" className={claseCampo} required />
                        </div>
                    </div>

                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                                <MapPin size={13} className="inline mr-1" />{esDescarga ? 'Centro de recepción *' : 'Puesto de expedición *'}
                            </label>
                            <select name="id_PuestoExpedicionRecepcion" value={formData.id_PuestoExpedicionRecepcion} onChange={handleChange} className={claseSelect} required disabled={loadingOpts}>
                                <option value="">Seleccione...</option>
                                {opciones.puestos.map(p => <option key={p.id_PuestoExpedicionRecepcion} value={p.id_PuestoExpedicionRecepcion}>{p.descripcion}</option>)}
                            </select>
                        </div>
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">Nro. de remito *</label>
                            <input type="number" name="nroRemito" value={formData.nroRemito} onChange={handleChange} min="1" step="1" placeholder="Ej: 1000123" className={claseCampo} required />
                        </div>
                    </div>

                    <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-1.5">Producto / Material *</label>
                        <select name="id_Producto" value={formData.id_Producto} onChange={handleChange} className={claseSelect} required disabled={loadingOpts}>
                            <option value="">Seleccione producto...</option>
                            {opciones.productos.map(p => <option key={p.id_Producto} value={p.id_Producto}>{p.descripcion}</option>)}
                        </select>
                    </div>

                    <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                            <Database size={13} className="inline mr-1" />{esDescarga ? 'Tanque de destino *' : 'Tanque de origen (opcional)'}
                        </label>
                        <select name="id_Tanque" value={formData.id_Tanque} onChange={handleChange} required={esDescarga} disabled={loadingOpts}
                            className={`${claseSelect} ${capacidadExcedida ? 'border-red-400' : ''}`}>
                            <option value="">{esDescarga ? 'Seleccione tanque...' : 'Sin tanque'}</option>
                            {opciones.tanques.map(t => (
                                <option key={t.id_Tanque} value={t.id_Tanque}>
                                    {t.codigo} — {t.descripcion} (disp.: {Number(t.capacidadDisponible).toLocaleString('es-AR')} L)
                                </option>
                            ))}
                        </select>
                        {capacidadExcedida && (
                            <p className="text-xs text-red-600 font-semibold mt-1 flex items-center gap-1">
                                <AlertCircle size={12} /> Capacidad insuficiente: {Math.floor(disponible).toLocaleString('es-AR')} L disponibles, se requieren {Math.ceil(litros).toLocaleString('es-AR')} L.
                            </p>
                        )}
                    </div>

                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">{esDescarga ? 'Proveedor (origen) *' : 'Cliente (destino) *'}</label>
                            <select name="id_RazonSocialCliente" value={formData.id_RazonSocialCliente} onChange={handleChange} className={claseSelect} required disabled={loadingOpts}>
                                <option value="">Seleccione...</option>
                                {opciones.razones.map(c => <option key={c.id_RazonSocial} value={c.id_RazonSocial}>{c.descripcion}</option>)}
                            </select>
                        </div>
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">Transportista *</label>
                            <select name="id_RazonSocialTransportista" value={formData.id_RazonSocialTransportista} onChange={handleChange} className={claseSelect} required disabled={loadingOpts}>
                                <option value="">Seleccione transportista...</option>
                                {(transportistas.length > 0 ? transportistas : opciones.razones).map(t => <option key={t.id_RazonSocial} value={t.id_RazonSocial}>{t.descripcion}</option>)}
                            </select>
                        </div>
                    </div>

                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">Vehículo (chasis) *</label>
                            <select name="id_Vehiculo" value={formData.id_Vehiculo} onChange={handleChange} className={claseSelect} required disabled={loadingOpts}>
                                <option value="">Seleccione vehículo...</option>
                                {opciones.vehiculos.map(v => <option key={v.id_Vehiculo} value={v.id_Vehiculo}>{v.patenteChasis}</option>)}
                            </select>
                        </div>
                        <div>
                            <label className="block text-sm font-semibold text-slate-700 mb-1.5">Conductor *</label>
                            <select name="id_Conductor" value={formData.id_Conductor} onChange={handleChange} className={claseSelect} required disabled={loadingOpts}>
                                <option value="">Seleccione conductor...</option>
                                {opciones.conductores.map(c => <option key={c.id_Conductor} value={c.id_Conductor}>{c.apellido}, {c.nombre}</option>)}
                            </select>
                        </div>
                    </div>

                    <MensajeError mensaje={error} />

                    <div className="flex gap-3 pt-2">
                        <button type="button" onClick={onClose} className="flex-1 py-2.5 rounded-xl border border-slate-200 text-slate-700 font-semibold text-sm hover:bg-slate-50 transition-colors">
                            Cancelar
                        </button>
                        <button type="submit" disabled={loading || loadingOpts || capacidadExcedida || !tipoElegido}
                            className={`flex-1 py-2.5 rounded-xl text-white font-bold text-sm transition-colors flex items-center justify-center gap-2 disabled:opacity-50 ${esDescarga ? 'bg-blue-600 hover:bg-blue-700' : 'bg-emerald-600 hover:bg-emerald-700'}`}>
                            {loading ? <Loader2 size={16} className="animate-spin" /> : <Save size={16} />}
                            {loading ? 'Guardando...' : esEdicion ? 'Guardar cambios' : 'Crear orden'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

// ─────────── Ver detalle (CU062 / CU068) ───────────
export const DetalleOrdenModal = ({ isOpen, onClose, orden }) => {
    if (!isOpen || !orden) return null;

    const detalle = orden.ordenDetalle;
    const ordenObj = detalle?.orden;
    const esDescarga = ordenObj?.id_TipoOrden === TIPO_DESCARGA;
    const tanque = detalle?.tanquesAlmacenamiento?.[0];

    const Campo = ({ label, value }) => (
        <div>
            <p className="text-[11px] font-bold text-slate-400 uppercase tracking-widest mb-0.5">{label}</p>
            <p className="text-sm font-semibold text-slate-700">{value || '—'}</p>
        </div>
    );

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
            <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto animate-in fade-in zoom-in-95 duration-200">
                <div className={`px-6 py-5 flex justify-between items-center rounded-t-2xl sticky top-0 bg-linear-to-r ${esDescarga ? 'from-blue-700 to-blue-600' : 'from-emerald-700 to-emerald-600'}`}>
                    <div className="flex items-center gap-3">
                        <div className="p-2 bg-white/10 rounded-xl"><Eye size={20} className="text-white" /></div>
                        <div>
                            <h3 className="text-lg font-bold text-white">Detalle — OT-{orden.id_OrdenTransporte}</h3>
                            <p className="text-xs text-white/60">{esDescarga ? 'Autorización de Descarga' : 'Orden de Entrega'}</p>
                        </div>
                    </div>
                    <button onClick={onClose} className="text-white/70 hover:text-white"><X size={22} /></button>
                </div>

                <div className="p-6 space-y-5">
                    <div className="grid grid-cols-2 gap-5">
                        <Campo label="Estado" value={estadoDe(orden).texto} />
                        <Campo label="Fecha planificada" value={formatoFecha(orden.fechaPlanificacion)} />
                        <Campo label={esDescarga ? 'Centro de recepción' : 'Puesto de expedición'} value={ordenObj?.puesto?.descripcion} />
                        <Campo label="Nro. de remito" value={detalle?.nroRemito} />
                        <Campo label="Producto / Material" value={detalle?.producto?.descripcion} />
                        <Campo label="Cantidad" value={detalle?.peso ? `${Number(detalle.peso).toLocaleString('es-AR')} KG` : undefined} />
                        <Campo label={esDescarga ? 'Tanque de destino' : 'Tanque de origen'}
                            value={tanque ? `Tanque #${tanque.id_Tanque} · ${Number(tanque.volumen).toLocaleString('es-AR')} L` : undefined} />
                        <Campo label="Transportista" value={orden.razonSocialTransportista?.descripcion} />
                        <Campo label="Vehículo (chasis)" value={orden.vehiculo?.patenteChasis} />
                        <Campo label="Conductor" value={orden.conductor ? `${orden.conductor.apellido}, ${orden.conductor.nombre}` : undefined} />
                    </div>

                    {orden.motivoReplanificacion && (
                        <div className="text-sm bg-amber-50 border border-amber-100 rounded-xl p-3">
                            <p className="font-bold text-amber-800">Replanificada el {formatoFecha(orden.fechaFinReplanificacion)}</p>
                            <p className="text-amber-700">Motivo: {orden.motivoReplanificacion}</p>
                        </div>
                    )}
                    {ordenObj?.id_EstadoOrden === ORDEN_ANULADO && (
                        <div className="text-sm bg-red-50 border border-red-100 rounded-xl p-3">
                            <p className="font-bold text-red-800">Anulada el {formatoFecha(ordenObj.fechaAnulacion)}</p>
                            <p className="text-red-700">Motivo: {ordenObj.motivoAnulacion}</p>
                        </div>
                    )}

                    <div className="flex justify-end pt-2">
                        <button onClick={onClose} className="px-6 py-2.5 rounded-xl border border-slate-200 text-slate-700 font-semibold text-sm hover:bg-slate-50 transition-colors">
                            Cerrar
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};

// ─────────── Cancelar (CU064 / CU070): la orden pasa a Anulado ───────────
export const CancelarOrdenModal = ({ isOpen, onClose, onSuccess, orden }) => {
    const [motivo, setMotivo] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        if (isOpen) { setMotivo(''); setError(''); }
    }, [isOpen]);

    if (!isOpen || !orden) return null;

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        if (!motivo.trim()) {
            setError('Indique el motivo de la cancelación.');
            return;
        }
        setLoading(true);
        try {
            await agent.OrdenesTransporte.cancelar(orden.id_OrdenTransporte, motivo.trim());
            onSuccess();
            onClose();
        } catch (err) {
            setError(getErrorMessage(err, 'No se pudo cancelar la orden.'));
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
            <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md animate-in fade-in zoom-in-95 duration-200">
                <div className="px-6 py-5 flex justify-between items-center rounded-t-2xl bg-linear-to-r from-red-700 to-red-600">
                    <div className="flex items-center gap-3">
                        <div className="p-2 bg-white/10 rounded-xl"><Ban size={20} className="text-white" /></div>
                        <div>
                            <h3 className="text-lg font-bold text-white">Cancelar orden</h3>
                            <p className="text-xs text-white/70">OT-{orden.id_OrdenTransporte} · {orden.ordenDetalle?.producto?.descripcion || 'Material'}</p>
                        </div>
                    </div>
                    <button onClick={onClose} className="text-white/70 hover:text-white"><X size={22} /></button>
                </div>
                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-1.5">Motivo de la anulación *</label>
                        <textarea value={motivo} onChange={(e) => { setMotivo(e.target.value); setError(''); }} rows={3} required
                            placeholder="Ingrese el motivo..." className={`${claseCampo} resize-none`} />
                    </div>
                    <MensajeError mensaje={error} />
                    <div className="flex gap-3 pt-2">
                        <button type="button" onClick={onClose} className="flex-1 py-2.5 rounded-xl border border-slate-200 text-slate-700 font-semibold text-sm hover:bg-slate-50 transition-colors">
                            Volver
                        </button>
                        <button type="submit" disabled={loading} className="flex-1 py-2.5 rounded-xl bg-red-600 text-white font-bold text-sm hover:bg-red-700 transition-colors flex items-center justify-center gap-2">
                            {loading ? <Loader2 size={16} className="animate-spin" /> : <Ban size={16} />}
                            {loading ? 'Cancelando...' : 'Confirmar cancelación'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

// ─────────── Replanificar (CU075): nueva fecha con motivo obligatorio ───────────
const LARGO_MAXIMO_MOTIVO_REPLANIFICACION = 500;

export const ReplanificarOrdenModal = ({ isOpen, onClose, onSuccess, orden }) => {
    const [fecha, setFecha] = useState('');
    const [motivo, setMotivo] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        if (isOpen) { setFecha(''); setMotivo(''); setError(''); }
    }, [isOpen]);

    if (!isOpen || !orden) return null;

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        if (!motivo.trim()) {
            setError('Indique el motivo de la replanificación.');
            return;
        }
        setLoading(true);
        try {
            await agent.OrdenesTransporte.replanificar(orden.id_OrdenTransporte, fechaParaApi(fecha), motivo.trim());
            onSuccess();
            onClose();
        } catch (err) {
            setError(getErrorMessage(err, 'No se pudo replanificar la orden.'));
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4">
            <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md animate-in fade-in zoom-in-95 duration-200">
                <div className="px-6 py-5 flex justify-between items-center rounded-t-2xl bg-linear-to-r from-amber-600 to-amber-500">
                    <div className="flex items-center gap-3">
                        <div className="p-2 bg-white/10 rounded-xl"><CalendarClock size={20} className="text-white" /></div>
                        <div>
                            <h3 className="text-lg font-bold text-white">Replanificar orden</h3>
                            <p className="text-xs text-white/80">OT-{orden.id_OrdenTransporte} · planificada para el {formatoFecha(orden.fechaPlanificacion)}</p>
                        </div>
                    </div>
                    <button onClick={onClose} className="text-white/70 hover:text-white"><X size={22} /></button>
                </div>
                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-1.5">Nueva fecha planificada *</label>
                        <input type="date" value={fecha} min={hoyISO()} onChange={(e) => { setFecha(e.target.value); setError(''); }} className={claseCampo} required />
                    </div>
                    <div>
                        <label className="block text-sm font-semibold text-slate-700 mb-1.5">Motivo *</label>
                        <textarea value={motivo} onChange={(e) => { setMotivo(e.target.value); setError(''); }} rows={3} required
                            maxLength={LARGO_MAXIMO_MOTIVO_REPLANIFICACION} placeholder="Motivo de la replanificación..." className={`${claseCampo} resize-none`} />
                    </div>
                    <MensajeError mensaje={error} />
                    <div className="flex gap-3 pt-2">
                        <button type="button" onClick={onClose} className="flex-1 py-2.5 rounded-xl border border-slate-200 text-slate-700 font-semibold text-sm hover:bg-slate-50 transition-colors">
                            Volver
                        </button>
                        <button type="submit" disabled={loading} className="flex-1 py-2.5 rounded-xl bg-amber-500 text-white font-bold text-sm hover:bg-amber-600 transition-colors flex items-center justify-center gap-2">
                            {loading ? <Loader2 size={16} className="animate-spin" /> : <CalendarClock size={16} />}
                            {loading ? 'Guardando...' : 'Replanificar'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};
