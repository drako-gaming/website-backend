create table games
(
	id bigint generated always as identity,
    status varchar(30) not null,
	options json not null,
	winner int,
	maximum_bet bigint,
    primary key(id)
);

ALTER TABLE games OWNER TO drakoapi;

create table wagers
(
    id bigint generated always as identity,
    game_id bigint not null,
    user_id bigint not null,
    amount bigint not null,
    option int not null,
    primary key(id),
    constraint fk_wagers_games foreign key(game_id) references games(id),
    constraint fk_wagers_users foreign key(user_id) references users(id),
    unique (game_id, user_id)
);

ALTER TABLE wagers OWNER TO drakoapi;

create table junk
(
    name varchar(30) not null,
    value bigint not null
);

alter table junk owner to drakoapi;
