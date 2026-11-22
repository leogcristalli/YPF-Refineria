import React, { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Scale, LogIn, Loader2, AlertCircle } from 'lucide-react';
import { getErrorMessage } from '../api/agent';
import { useAuth } from './AuthContext';

const LoginPage = () => {
    const { sesion, login } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();
    const [idRed, setIdRed] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);

    // Llegó acá por un 401 (sesión vencida) desde el interceptor de agent.js.
    const vencida = new URLSearchParams(location.search).get('vencida') === '1';
    const destino = location.state?.desde || '/';

    if (sesion) return <Navigate to={destino} replace />;

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        setLoading(true);
        try {
            await login(idRed.trim(), password);
            navigate(destino, { replace: true });
        } catch (err) {
            setError(getErrorMessage(err, 'No se pudo iniciar sesión. Verifique que el servidor esté disponible.'));
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="min-h-screen flex items-center justify-center bg-linear-to-br from-slate-900 via-slate-900 to-blue-950 p-4">
            <div className="w-full max-w-sm bg-white rounded-2xl shadow-2xl overflow-hidden">
                <div className="px-8 pt-8 pb-6 flex items-center gap-3 border-b border-slate-100">
                    <div className="w-12 h-12 bg-blue-600 rounded-xl flex items-center justify-center shadow-lg shadow-blue-500/20">
                        <Scale className="text-white" size={24} />
                    </div>
                    <div>
                        <h1 className="text-xl font-bold tracking-tight text-slate-800">CILP <span className="text-blue-600">Refinería</span></h1>
                        <p className="text-xs text-slate-500">Sistema de Balanzas · Iniciar sesión</p>
                    </div>
                </div>

                <form onSubmit={handleSubmit} className="p-8 space-y-4">
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
                            className="w-full p-2.5 border border-slate-200 rounded-xl text-sm font-medium focus:ring-2 focus:ring-blue-500/20 focus:border-blue-400 outline-none"
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
                            className="w-full p-2.5 border border-slate-200 rounded-xl text-sm font-medium focus:ring-2 focus:ring-blue-500/20 focus:border-blue-400 outline-none"
                        />
                    </div>

                    {error && (
                        <p className="text-xs text-red-700 bg-red-50 border border-red-100 rounded-xl p-3 flex items-center gap-2">
                            <AlertCircle size={14} className="shrink-0" /> {error}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={loading}
                        className="w-full py-2.5 rounded-xl bg-blue-600 text-white font-bold text-sm hover:bg-blue-700 transition-colors shadow-lg shadow-blue-500/20 flex items-center justify-center gap-2 disabled:opacity-60"
                    >
                        {loading ? <Loader2 size={16} className="animate-spin" /> : <LogIn size={16} />}
                        {loading ? 'Ingresando...' : 'Ingresar'}
                    </button>
                </form>
            </div>
        </div>
    );
};

export default LoginPage;
