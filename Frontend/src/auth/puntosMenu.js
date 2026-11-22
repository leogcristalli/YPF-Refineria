// Ids de t_CasoDeUso: un punto de menú por pantalla (script_initdata_t_casodeuso.sql).
// Son los mismos valores que Backend/Auth/PuntoMenu.cs.
export const PUNTOS = {
    PESADAS: 1,
    ORDENES_TRANSPORTE: 2,
    AUTORIZACIONES_DESCARGA: 3,
    ORDENES_ENTREGA: 4,
    REPORTES: 5,
    VEHICULOS: 6,
    CONDUCTORES: 7,
    PRODUCTOS: 8,
    RAZONES_SOCIALES: 9,
    BASCULAS: 10,
    TANQUES: 11,
    PUESTOS: 12,
    DISPOSITIVOS: 13,
    PUNTOS_MENU: 14,
    ROLES: 15,
    USUARIOS: 16,
};

// Puntos de menú que habilitan cada pantalla (alcanza con uno). La usan las rutas (App.jsx),
// el menú lateral (MainLayout.jsx) y el login. Inicio ('/') no lleva punto: la ven todos.
export const PUNTOS_POR_RUTA = {
    '/pesadas': [PUNTOS.PESADAS],
    '/ordenes-transporte': [PUNTOS.ORDENES_TRANSPORTE],
    '/planificacion': [PUNTOS.AUTORIZACIONES_DESCARGA, PUNTOS.ORDENES_ENTREGA],
    '/reportes': [PUNTOS.REPORTES],
    '/vehiculos': [PUNTOS.VEHICULOS],
    '/conductores': [PUNTOS.CONDUCTORES],
    '/productos': [PUNTOS.PRODUCTOS],
    '/razones-sociales': [PUNTOS.RAZONES_SOCIALES],
    '/basculas': [PUNTOS.BASCULAS],
    '/tanques': [PUNTOS.TANQUES],
    '/puestos': [PUNTOS.PUESTOS],
    '/dispositivos': [PUNTOS.DISPOSITIVOS],
    '/puntos-menu': [PUNTOS.PUNTOS_MENU],
    '/roles': [PUNTOS.ROLES],
    '/usuarios': [PUNTOS.USUARIOS],
};

// La ruta no exige punto o el usuario tiene alguno de los que exige.
export const rutaPermitida = (ruta, puntosUsuario = []) => {
    const requeridos = PUNTOS_POR_RUTA[ruta];
    return !requeridos || requeridos.some(p => puntosUsuario.includes(p));
};
