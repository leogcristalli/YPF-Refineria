import React from 'react';
import { Routes, Route } from 'react-router-dom';
import MainLayout from './layout/MainLayout';
import LoginPage from './auth/LoginPage';
import { RequireAuth, Permitido } from './auth/AuthContext';
import { PUNTOS_POR_RUTA } from './auth/puntosMenu';
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

// Ruta de una pantalla protegida por los puntos de menú de PUNTOS_POR_RUTA.
const pantalla = (path, element) => (
  <Route path={path} element={<Permitido puntos={PUNTOS_POR_RUTA[`/${path}`]}>{element}</Permitido>} />
);

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route path="/" element={<RequireAuth><MainLayout /></RequireAuth>}>
        {/* Inicio: cualquier usuario logueado */}
        <Route index element={<Dashboard />} />

        {/* Operaciones */}
        {pantalla('pesadas', <PesadasPage />)}
        {pantalla('ordenes-transporte', <OrdenesTransportePage />)}
        {pantalla('planificacion', <AutorizacionesPage />)}
        {pantalla('reportes', <ReportesPage />)}

        {/* Maestros */}
        {pantalla('vehiculos', <VehiculosPage />)}
        {pantalla('conductores', <ConductoresPage />)}
        {pantalla('productos', <ProductosPage />)}
        {pantalla('razones-sociales', <RazonesSocialesPage />)}
        {pantalla('basculas', <BasculasPage />)}
        {pantalla('tanques', <TanquesPage />)}
        {pantalla('puestos', <PuestosPage />)}
        {pantalla('dispositivos', <DispositivosPage />)}
        {pantalla('puntos-menu', <PuntosMenuPage />)}
        {pantalla('roles', <RolesPage />)}
        {pantalla('usuarios', <UsuariosPage />)}
      </Route>
    </Routes>
  );
}

export default App;
