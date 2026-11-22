// Sesión del usuario logueado (token JWT, usuario, roles y puntos de menú).
// Se guarda en sessionStorage: se borra al cerrar la pestaña del navegador.
const CLAVE = 'cilp.sesion';

export const leerSesion = () => {
    try {
        const sesion = JSON.parse(sessionStorage.getItem(CLAVE));
        if (!sesion?.token) return null;
        // Token vencido: la sesión ya no sirve.
        if (sesion.expira && new Date(sesion.expira) <= new Date()) {
            borrarSesion();
            return null;
        }
        return sesion;
    } catch {
        return null;
    }
};

export const guardarSesion = (sesion) => {
    try {
        sessionStorage.setItem(CLAVE, JSON.stringify(sesion));
    } catch {
        // Sin almacenamiento disponible la sesión dura solo mientras no se recargue la página.
    }
};

export const borrarSesion = () => {
    try {
        sessionStorage.removeItem(CLAVE);
    } catch {
        // nada que borrar
    }
};
