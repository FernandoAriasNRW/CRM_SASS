import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminTeamsComponent, type TeamDto } from './admin-teams.component';
import { ApiService } from '../../../core/api.service';

/**
 * La edición de un equipo en administración.
 *
 * Guardar manda la lista de miembros entera y la API la aplica tal cual. La edición abría sin
 * nadie marcado —la API no decía quiénes eran—, así que guardar sólo el nombre habría dejado el
 * equipo vacío. Lo que fijan estas pruebas es que la edición parte de los miembros que hay.
 */
describe('AdminTeamsComponent', () => {
  const TEAM: TeamDto = {
    id: 't1', name: 'Soporte', description: 'Primer nivel', memberCount: 2, memberIds: ['u1', 'u2'],
  };

  let api: jasmine.SpyObj<ApiService>;
  let fixture: ComponentFixture<AdminTeamsComponent>;
  let component: AdminTeamsComponent;

  beforeEach(async () => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['get', 'put', 'post', 'delete']);
    api.get.and.callFake(((path: string) => of(path === '/teams' ? [TEAM] : [])) as never);
    api.put.and.returnValue(of(undefined) as never);

    await TestBed.configureTestingModule({
      imports: [AdminTeamsComponent],
      providers: [{ provide: ApiService, useValue: api }],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminTeamsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('al editar, marca a los miembros que ya tiene el equipo', () => {
    component.openEditModal(TEAM);

    expect(component.selectedMemberIds()).toEqual(['u1', 'u2']);
  });

  it('guardar sin tocar a nadie manda los mismos miembros', () => {
    component.openEditModal(TEAM);
    component.formName = 'Soporte renombrado';
    component.saveTeam();

    expect(api.put).toHaveBeenCalledWith('/teams/t1', jasmine.objectContaining({
      name: 'Soporte renombrado',
      memberIds: ['u1', 'u2'],
    }));
  });

  it('quitar a alguien lo saca de la lista que se guarda', () => {
    component.openEditModal(TEAM);
    component.toggleMemberSelection('u1');
    component.saveTeam();

    expect(api.put).toHaveBeenCalledWith('/teams/t1', jasmine.objectContaining({ memberIds: ['u2'] }));
  });
});
