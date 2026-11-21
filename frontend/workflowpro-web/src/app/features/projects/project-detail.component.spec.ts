import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ProjectDetailComponent } from './project-detail.component';
import { ProjectService } from '../../core/projects/project.service';
import { AuthService } from '../../core/auth/auth.service';
import { ProjectDetail } from '../../core/projects/project.models';

describe('ProjectDetailComponent', () => {
  let component: ProjectDetailComponent;
  let fixture: ComponentFixture<ProjectDetailComponent>;
  let projectService: ProjectService;
  let authService: AuthService;

  const mockDetail: ProjectDetail = {
    id: 'proj-123',
    projectCode: 'WFP-DEV',
    name: 'Dev Project',
    description: 'Dev scope',
    status: 'Active',
    createdById: 'user-admin',
    createdByName: 'Admin User',
    createdAtUtc: '2026-11-21',
    members: [
      {
        userId: 'user-admin',
        displayName: 'Admin User',
        email: 'admin@workflowpro.local',
        projectRole: 'Owner',
        joinedAtUtc: '2026-11-21'
      }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? 'proj-123' : null)
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectDetailComponent);
    component = fixture.componentInstance;
    projectService = TestBed.inject(ProjectService);
    authService = TestBed.inject(AuthService);
  });

  it('should load project detail on init', () => {
    vi.spyOn(projectService, 'getById').mockReturnValue(of(mockDetail));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.project()?.projectCode).toBe('WFP-DEV');
    expect(component.project()?.members.length).toBe(1);
  });

  it('canEdit should return true if user is Owner', () => {
    component.project.set(mockDetail);
    vi.spyOn(authService, 'hasRole').mockReturnValue(false);
    vi.spyOn(authService, 'currentUser').mockReturnValue({
      id: 'user-admin',
      email: 'admin@workflowpro.local',
      displayName: 'Admin User',
      roles: ['Manager']
    });

    expect(component.canEdit()).toBe(true);
    expect(component.canArchive()).toBe(true);
  });
});
