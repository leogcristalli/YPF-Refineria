import React, { createContext, useContext, useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { ShieldOff } from 'lucide-react';
import agent from '../api/agent';
import { leerSesion, guardarSesion, borrarSesion } from './sesion';

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
    const [sesion, setSesion] = useState(() => leerSesion());

    // Guarda la sesión que devolvió el servidor (login sin doble factor o verificación del código).
    const iniciarSesion = (respuesta) => {
        guardarSesion(respuesta);
        setSesion(respuesta);
        return respuesta;
    };

    // Con doble factor el servidor no devuelve la sesión sino un desafío (requiere2fa o
    // requiereEnrolamiento): la pantalla de login lo completa con el código y llama a iniciarSesion.
    const login = async (idRed, password) => {
        const respuesta = await agent.Auth.login(idRed, password);
        if (respuesta.requiere2fa || respuesta.requiereEnrolamiento) return respuesta;
        return iniciarSesion(respuesta);
    };

    const logout = () => {
        borrarSesion();
        setSesion(null);
    };

    // El usuario tiene alguno de los puntos de menú indicados.
    const tienePunto = (...puntos) => !!sesion && puntos.some(p => sesion.puntosMenu?.includes(p));

    return (
        <AuthContext.Provider value={{ sesion, login, iniciarSesion, logout, tienePunto }}>
            {children}
        </AuthContext.Provider>
    );
};

export const useAuth = () => useContext(AuthContext);

// Rutas que exigen sesión: sin sesión, al login (y vuelve a la página pedida después).
export const RequireAuth = ({ children }) => {
    const { sesion } = useAuth();
    const location = useLocation();
    if (!sesion) return <Navigate to="/login" replace state={{ desde: location.pathname }} />;
    return children;
};

// Pantalla que exige alguno de los puntos de menú indicados.
export const Permitido = ({ puntos, children }) => {
    const { tienePunto } = useAuth();
    if (tienePunto(...puntos)) return children;
    return (
        <div className="flex flex-col items-center justify-center text-center py-24 gap-3 text-slate-500">
            <ShieldOff size={40} className="text-slate-300" />
            <h2 className="text-lg font-bold text-slate-700">No tiene permiso para ver esta pantalla</h2>
            <p className="text-sm">Sus roles no incluyen este punto de menú. Consulte al administrador del sistema.</p>
        </div>
    );
};
