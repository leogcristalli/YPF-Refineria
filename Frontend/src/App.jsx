import React from 'react';
import { Routes, Route } from 'react-router-dom';
import MainLayout from './layout/MainLayout';
import LoginPage from './auth/LoginPage';
import { RequireAuth, Permitido } from './auth/AuthContext';
import { PUNTOS } from './auth/puntosMenu';
import Dashboard from './features/dashboard/Dashboard';
import PesadasPage from './features/pesadas/PesadasPage';
import OrdenesTransportePage from './features/masters/OrdenesTransportePage';
import VehiculosPage from './features/masters/VehiculosPage';
import ConductoresPage from './features/masters/ConductoresPage';
import ProductosPage from './features/masters/ProductosPage';
import RazonesSocialesPage from './features/masters/RazonesSocialesPage';
import BasculasPage from './features/masters/BasculasPage';
import UsuariosPage from './features/masters/UsuariosPage';
import TanquesPage from './features/masters/TanquesPage';
import DispositivosPage from './features/masters/DispositivosPage';
import PuestosPage from './features/masters/PuestosPage';
import PuntosMenuPage from './features/masters/PuntosMenuPage';
import RolesPage from './features/masters/RolesPage';
import AutorizacionesPage from './features/planificacion/AutorizacionesPage';
import ReportesPage from './features/reportes/ReportesPage';

// Ruta de una pantalla con los puntos de menú que la habilitan.
const pantalla = (path, puntos, element) => (
  <Route path={path} element={<Permitido puntos={puntos}>{element}</Permitido>} />
);

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route path="/" element={<RequireAuth><MainLayout /></RequireAuth>}>
        {/* Inicio: cualquier usuario logueado */}
        <Route index element={<Dashboard />} />

        {/* Operaciones */}
        {pantalla('pesadas', [PUNTOS.PESADAS], <PesadasPage />)}
        {pantalla('ordenes-transporte', [PUNTOS.ORDENES_TRANSPORTE], <OrdenesTransportePage />)}
        {pantalla('planificacion', [PUNTOS.AUTORIZACIONES_DESCARGA, PUNTOS.ORDENES_ENTREGA], <AutorizacionesPage />)}
        {pantalla('reportes', [PUNTOS.REPORTES], <ReportesPage />)}

        {/* Maestros */}
        {pantalla('vehiculos', [PUNTOS.VEHICULOS], <VehiculosPage />)}
        {pantalla('conductores', [PUNTOS.CONDUCTORES], <ConductoresPage />)}
        {pantalla('productos', [PUNTOS.PRODUCTOS], <ProductosPage />)}
        {pantalla('razones-sociales', [PUNTOS.RAZONES_SOCIALES], <RazonesSocialesPage />)}
        {pantalla('basculas', [PUNTOS.BASCULAS], <BasculasPage />)}
        {pantalla('tanques', [PUNTOS.TANQUES], <TanquesPage />)}
        {pantalla('puestos', [PUNTOS.PUESTOS], <PuestosPage />)}
        {pantalla('dispositivos', [PUNTOS.DISPOSITIVOS], <DispositivosPage />)}
        {pantalla('puntos-menu', [PUNTOS.PUNTOS_MENU], <PuntosMenuPage />)}
        {pantalla('roles', [PUNTOS.ROLES], <RolesPage />)}
        {pantalla('usuarios', [PUNTOS.USUARIOS], <UsuariosPage />)}
      </Route>
    </Routes>
  );
}

export default App;
