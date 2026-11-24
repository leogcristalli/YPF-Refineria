import React, { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Scale, LogIn, Loader2, AlertCircle, ShieldCheck, ArrowLeft } from 'lucide-react';
import { QRCodeSVG } from 'qrcode.react';
import agent, { getErrorMessage } from '../api/agent';
import { useAuth } from './AuthContext';
import { rutaPermitida } from './puntosMenu';

const inputClass = 'w-full p-2.5 border border-slate-200 rounded-xl text-sm font-medium focus:ring-2 focus:ring-blue-500/20 focus:border-blue-400 outline-none';
const botonClass = 'w-full py-2.5 rounded-xl bg-blue-600 text-white font-bold text-sm hover:bg-blue-700 transition-colors shadow-lg shadow-blue-500/20 flex items-center justify-center gap-2 disabled:opacity-60';

// Inicio de sesión en hasta dos pasos:
//   1. 'credenciales': ID de red y contraseña.
//   2. Si el usuario usa doble factor (2FA):
//        'codigo'  -> escribe el código de 6 dígitos de su aplicación autenticadora.
//        'enrolar' -> primera vez: escanea el QR, y confirma con un código para activarlo.
const LoginPage = () => {
    const { sesion, login, iniciarSesion } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();
    const [idRed, setIdRed] = useState('');
    const [password, setPassword] = useState('');
    const [paso, setPaso] = useState('credenciales');
    const [desafio, setDesafio] = useState('');
    const [enrolamiento, setEnrolamiento] = useState(null);   // { secreto, uri }
    const [codigo, setCodigo] = useState('');
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);

    // Llegó acá por un 401 (sesión vencida) desde el interceptor de agent.js.
    const vencida = new URLSearchParams(location.search).get('vencida') === '1';
    // Última pantalla pedida antes del login; si los roles del usuario no la permiten, Inicio.
    const desde = location.state?.desde || '/';
    const destinoPara = (s) => (rutaPermitida(desde, s?.puntosMenu) ? desde : '/');

    if (sesion) return <Navigate to={destinoPara(sesion)} replace />;

    const volver = () => {
        setPaso('credenciales');
        setDesafio('');
        setEnrolamiento(null);
        setCodigo('');
        setPassword('');
        setError('');
    };

    const handleCredenciales = async (e) => {
        e.preventDefault();
        setError('');
        setLoading(true);
        try {
            const respuesta = await login(idRed.trim(), password);

            if (respuesta.requiere2fa) {
                setDesafio(respuesta.desafio);
                setPaso('codigo');
            } else if (respuesta.requiereEnrolamiento) {
                const datos = await agent.Auth.iniciarEnrolamiento(respuesta.desafio);
                setDesafio(respuesta.desafio);
                setEnrolamiento(datos);
                setPaso('enrolar');
            } else {
                navigate(destinoPara(respuesta), { replace: true });
            }
        } catch (err) {
            setError(getErrorMessage(err, 'No se pudo iniciar sesión. Verifique que el servidor esté disponible.'));
        } finally {
            setLoading(false);
        }
    };

    const handleCodigo = async (e) => {
        e.preventDefault();
        setError('');
        setLoading(true);
        try {
            const nueva = paso === 'enrolar'
                ? await agent.Auth.confirmarEnrolamiento(desafio, codigo)
                : await agent.Auth.verificar2fa(desafio, codigo);
            iniciarSesion(nueva);
            navigate(destinoPara(nueva), { replace: true });
        } catch (err) {
            setCodigo('');
            setError(getErrorMessage(err, 'No se pudo verificar el código.'));
        } finally {
            setLoading(false);
        }
    };

    const campoCodigo = (
        <div>
            <label htmlFor="codigo" className="block text-sm font-semibold text-slate-700 mb-1.5">Código de 6 dígitos</label>
            <input
                id="codigo"
                type="text"
                inputMode="numeric"
                autoComplete="one-time-code"
                value={codigo}
                onChange={e => setCodigo(e.target.value.replace(/[^0-9 ]/g, ''))}
                placeholder="000000"
                autoFocus
                required
                maxLength={7}
                className={`${inputClass} text-center text-xl tracking-[0.4em] font-mono`}
            />
        </div>
    );

    const mensajeError = error && (
        <p className="text-xs text-red-700 bg-red-50 border border-red-100 rounded-xl p-3 flex items-center gap-2">
            <AlertCircle size={14} className="shrink-0" /> {error}
        </p>
    );

    const botonVolver = (
        <button type="button" onClick={volver} className="w-full text-xs font-semibold text-slate-500 hover:text-slate-700 flex items-center justify-center gap-1.5 pt-1">
            <ArrowLeft size={13} /> Volver al inicio de sesión
        </button>
    );

    const subtitulo = paso === 'credenciales'
        ? 'Sistema de Balanzas · Iniciar sesión'
        : paso === 'enrolar' ? 'Configurar doble factor' : 'Verificación en dos pasos';

    return (
        <div className="min-h-screen flex items-center justify-center bg-linear-to-br from-slate-900 via-slate-900 to-blue-950 p-4">
            <div className="w-full max-w-sm bg-white rounded-2xl shadow-2xl overflow-hidden">
                <div className="px-5 sm:px-8 pt-6 sm:pt-8 pb-5 sm:pb-6 flex items-center gap-3 border-b border-slate-100">
                    <div className="w-12 h-12 bg-blue-600 rounded-xl flex items-center justify-center shadow-lg shadow-blue-500/20">
                        <Scale className="text-white" size={24} />
                    </div>
                    <div>
                        <h1 className="text-xl font-bold tracking-tight text-slate-800">CILP <span className="text-blue-600">Refinería</span></h1>
                        <p className="text-xs text-slate-500">{subtitulo}</p>
                    </div>
                </div>

                {paso === 'credenciales' && (
                    <form onSubmit={handleCredenciales} className="p-5 sm:p-8 space-y-4">
                        {vencida && !error && (
                            <p className="text-xs text-amber-700 bg-amber-50 border border-amber-100 rounded-xl p-3">
                                La sesión venció o no es válida. Vuelva a iniciar sesión.
                            </p>
                        )}
                        <div>
                            <label htmlFor="idRed" className="block text-sm font-semibold text-slate-700 mb-1.5">ID de red</label>
                            <input
                                id="idRed"
                                type="text"
                                value={idRed}
                                onChange={e => setIdRed(e.target.value)}
                                placeholder="YPF\USUARIO"
                                autoComplete="username"
                                autoFocus
                                required
                                maxLength={50}
                                className={inputClass}
                            />
                        </div>
                        <div>
                            <label htmlFor="password" className="block text-sm font-semibold text-slate-700 mb-1.5">Contraseña</label>
                            <input
                                id="password"
                                type="password"
                                value={password}
                                onChange={e => setPassword(e.target.value)}
                                autoComplete="current-password"
                                required
                                className={inputClass}
                            />
                        </div>

                        {mensajeError}

                        <button type="submit" disabled={loading} className={botonClass}>
                            {loading ? <Loader2 size={16} className="animate-spin" /> : <LogIn size={16} />}
                            {loading ? 'Ingresando...' : 'Ingresar'}
                        </button>
                    </form>
                )}

                {paso === 'codigo' && (
                    <form onSubmit={handleCodigo} className="p-5 sm:p-8 space-y-4">
                        <p className="text-sm text-slate-600">
                            Abra su aplicación autenticadora y escriba el código de 6 dígitos de <strong>CILP Refinería</strong>.
                        </p>
                        {campoCodigo}
                        {mensajeError}
                        <button type="submit" disabled={loading || codigo.replace(/\D/g, '').length !== 6} className={botonClass}>
                            {loading ? <Loader2 size={16} className="animate-spin" /> : <ShieldCheck size={16} />}
                            {loading ? 'Verificando...' : 'Verificar e ingresar'}
                        </button>
                        {botonVolver}
                    </form>
                )}

                {paso === 'enrolar' && enrolamiento && (
                    <form onSubmit={handleCodigo} className="p-5 sm:p-8 space-y-4">
                        <p className="text-sm text-slate-600">
                            Su perfil requiere <strong>doble factor de autenticación</strong>. Configúrelo una sola vez:
                        </p>
                        <ol className="text-xs text-slate-600 space-y-1.5 list-decimal pl-4">
                            <li>Instale una aplicación autenticadora en su teléfono (Google Authenticator, Microsoft Authenticator o Authy).</li>
                            <li>Agregue una cuenta y escanee este código QR.</li>
                            <li>Escriba abajo el código de 6 dígitos que muestra la aplicación.</li>
                        </ol>

                        <div className="flex justify-center">
                            <div className="p-3 bg-white border border-slate-200 rounded-xl">
                                <QRCodeSVG value={enrolamiento.uri} size={Math.min(168, typeof window !== 'undefined' ? window.innerWidth - 96 : 168)} />
                            </div>
                        </div>

                        <div className="text-center">
                            <p className="text-[11px] text-slate-500">¿No puede escanearlo? Ingrese esta clave a mano:</p>
                            <p className="font-mono text-sm font-bold text-slate-700 tracking-wider select-all break-all">{enrolamiento.secreto}</p>
                        </div>

                        {campoCodigo}
                        {mensajeError}
                        <button type="submit" disabled={loading || codigo.replace(/\D/g, '').length !== 6} className={botonClass}>
                            {loading ? <Loader2 size={16} className="animate-spin" /> : <ShieldCheck size={16} />}
                            {loading ? 'Verificando...' : 'Verificar e ingresar'}
                        </button>
                        {botonVolver}
                    </form>
                )}
            </div>
        </div>
    );
};

export default LoginPage;
