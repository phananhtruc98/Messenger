import { Component, OnDestroy, OnInit, ViewEncapsulation } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';

import { ChatService } from '../../../chat.service';
import { User, Status } from 'app/apps/model/user.model';
import { UserProfileModel } from 'app/account/user-profile/user-profile.model';
import { UserProfileService } from 'app/core/identity/userprofile.service';

@Component({
    selector: 'chat-user-sidenav',
    templateUrl: './user.component.html',
    styleUrls: ['./user.component.scss'],
    encapsulation: ViewEncapsulation.None
})
export class ChatUserSidenavComponent implements OnInit, OnDestroy {
    user: User;
    beforeUser: any;
    userForm: FormGroup;
    contenteditable = false;
    _userProfileModel: UserProfileModel;

    // Private
    private _unsubscribeAll: Subject<any>;

    /**
     * Constructor
     *
     * @param {ChatService} _chatService
     */
    constructor(
        private _chatService: ChatService,
        private _userProfileService: UserProfileService
    ) {
        // Set the private defaults
        this._unsubscribeAll = new Subject();
    }

    // -----------------------------------------------------------------------------------------------------
    // @ Lifecycle hooks
    // -----------------------------------------------------------------------------------------------------

    /**
     * On init
     */
    ngOnInit(): void {
        this._userProfileModel = this._userProfileService.CurrentUserProfileModel();
        this._userProfileService.getUserById(this._userProfileModel.UserName).subscribe((data) => {

            this.user = data
            // this.user.mood = 1;
            // this.user.status = 1;
            this.beforeUser = Object.assign({}, this.user);

            // this.user = this._chatService.user;
            this.userForm = new FormGroup({
                mood: new FormControl(this.user.mood),
                status: new FormControl(Status[this.user.status])
            });

            this.userForm.valueChanges
                .pipe(
                    takeUntil(this._unsubscribeAll),
                    debounceTime(500),
                    distinctUntilChanged()
                )
                .subscribe(data => {
                    this.user.mood = data.mood;
                    this.user.status = data.status;
                    this._chatService.updateUserData(this.user);
                });

        });


    }

    /**
     * On destroy
     */
    ngOnDestroy(): void {
        // Unsubscribe from all subscriptions
        this._unsubscribeAll.next();
        this._unsubscribeAll.complete();
    }

    // -----------------------------------------------------------------------------------------------------
    // @ Public methods
    // -----------------------------------------------------------------------------------------------------

    /**
     * Change left sidenav view
     *
     * @param view
     */
    changeLeftSidenavView(view): void {
        this._chatService.onLeftSidenavViewChanged.next(view);
    }
    focusFunction() {
        this.contenteditable = true;
    }
    focusOutFunction() {
        this.contenteditable = false;
        debugger;
        this._userProfileService.editUser(this._userProfileModel.UserName, this.user).subscribe((data) => {
            console.log(data)
        });
    }
}
