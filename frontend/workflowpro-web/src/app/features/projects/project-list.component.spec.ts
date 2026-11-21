import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ProjectListComponent } from './project-list.component';
import { ProjectService } from '../../core/projects/project.service';
import { AuthService } from '../../core/auth/auth.service';

describe('ProjectListComponent', () => {
  let component: ProjectListComponent;
  let fixture: ComponentFixture<ProjectListComponent>;
  let projectService: ProjectService;
  let authService: AuthService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectListComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectListComponent);
    component = fixture.componentInstance;
    projectService = TestBed.inject(ProjectService);
    authService = TestBed.inject(AuthService);
  });

  it('should create and load projects on init', () => {
    const listSpy = vi.spyOn(projectService, 'list').mockReturnValue(of({
      items: [{
        id: '1',
        projectCode: 'P-1',
        name: 'Project 1',
        status: 'Active',
        memberCount: 1,
        createdAtUtc: '2026-11-21'
      }],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    }));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(listSpy).toHaveBeenCalled();
    expect(component.projects().length).toBe(1);
    expect(component.projects()[0].name).toBe('Project 1');
  });

  it('canCreateProject returns true for Administrator and Manager', () => {
    const hasRoleSpy = vi.spyOn(authService, 'hasAnyRole').mockReturnValue(true);

    expect(component.canCreateProject()).toBe(true);
    expect(hasRoleSpy).toHaveBeenCalledWith(['Administrator', 'Manager']);
  });

  it('openCreateModal resets form and opens modal', () => {
    component.openCreateModal();
    expect(component.showCreateModal()).toBe(true);
    expect(component.createForm.valid).toBe(false);
  });
});
